using System.Collections.Concurrent;
using System.Threading.Channels;
using Serilog;

namespace PKVault.Core.BackupProviders;

public enum BackupProviderPushState
{
    PENDING,
    SUCCESS,
    FAILED,
}

public record BackupProviderStatusDTO(
    string ProviderId,
    BackupProviderPushState State,
    DateTime? LastBackupCreatedAt,
    DateTime? LastAttemptAt,
    string? LastError
);

/// <summary>
/// Queues created backups and pushes them to every enabled <see cref="IBackupProvider"/>,
/// one backup at a time, on a background worker. Failures are retried, logged and kept
/// in <see cref="GetStatus"/>; they are never thrown back to the caller.
/// </summary>
public sealed class BackupProviderDispatcher : IAsyncDisposable, IDisposable
{
    public static readonly TimeSpan[] DefaultRetryDelays = [ TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30) ];

    private readonly IReadOnlyList<IBackupProvider> providers;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan[] retryDelays;

    private readonly Channel<BackupArtifact> queue = Channel.CreateUnbounded<BackupArtifact>(new() { SingleReader = true });
    private readonly ConcurrentDictionary<string, BackupProviderStatusDTO> status = new();
    private readonly CancellationTokenSource cts = new();
    private readonly Task worker;
    private int pendingCount;

    public BackupProviderDispatcher(IEnumerable<IBackupProvider> providers, TimeProvider timeProvider)
        : this(providers, timeProvider, DefaultRetryDelays) { }

    public BackupProviderDispatcher(IEnumerable<IBackupProvider> providers, TimeProvider timeProvider, TimeSpan[] retryDelays)
    {
        this.providers = [ .. providers ];
        this.timeProvider = timeProvider;
        this.retryDelays = retryDelays;
        worker = Task.Run(RunWorker);
    }

    public bool HasProviders => providers.Count > 0;

    /// <summary>True when at least one provider is configured (avoids building snapshots for nothing).</summary>
    public bool AnyEnabled() => providers.Any(provider =>
    {
        try
        {
            return provider.IsEnabled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Backup provider {provider.Id}: IsEnabled failed");
            return false;
        }
    });

    /// <summary>Adds a backup to the queue. Returns immediately.</summary>
    public void Enqueue(BackupArtifact artifact)
    {
        if (!HasProviders)
            return;

        Interlocked.Increment(ref pendingCount);
        if (!queue.Writer.TryWrite(artifact))
        {
            Interlocked.Decrement(ref pendingCount);
            Log.Warning($"Backup providers queue closed, {artifact.Name} not pushed");
        }
    }

    public List<BackupProviderStatusDTO> GetStatus() => [ .. status.Values.OrderBy(s => s.ProviderId) ];

    /// <summary>Waits until every queued backup was handled (tests, app shutdown).</summary>
    public async Task WaitForIdle(CancellationToken cancellationToken = default)
    {
        while (Volatile.Read(ref pendingCount) > 0)
            await Task.Delay(10, cancellationToken);
    }

    private async Task RunWorker()
    {
        try
        {
            await foreach (var artifact in queue.Reader.ReadAllAsync(cts.Token))
            {
                try
                {
                    foreach (var provider in providers)
                        await PushWithRetry(provider, artifact);
                }
                finally
                {
                    Interlocked.Decrement(ref pendingCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PushWithRetry(IBackupProvider provider, BackupArtifact artifact)
    {
        bool enabled;
        try
        {
            enabled = provider.IsEnabled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Backup provider {provider.Id}: IsEnabled failed");
            enabled = false;
        }
        if (!enabled)
            return;

        SetStatus(provider.Id, BackupProviderPushState.PENDING, artifact, null);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var _ = Log.Logger.Time($"Backup provider {provider.Id}: push {artifact.Name} ({artifact.Size} bytes)");
                await provider.Push(artifact, cts.Token);
                SetStatus(provider.Id, BackupProviderPushState.SUCCESS, artifact, null);
                return;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var canRetry = attempt < retryDelays.Length;
                Log.Warning(ex, $"Backup provider {provider.Id}: push failed (attempt {attempt + 1}{(canRetry ? ", will retry" : ", giving up")})");

                if (!canRetry)
                {
                    SetStatus(provider.Id, BackupProviderPushState.FAILED, artifact, ex.Message);
                    return;
                }

                await Task.Delay(retryDelays[ attempt ], timeProvider, cts.Token);
            }
        }
    }

    private void SetStatus(string providerId, BackupProviderPushState state, BackupArtifact artifact, string? error)
    {
        status[ providerId ] = new(
            ProviderId: providerId,
            State: state,
            LastBackupCreatedAt: artifact.CreatedAt,
            LastAttemptAt: timeProvider.GetUtcNow().UtcDateTime,
            LastError: error
        );
    }

    private int disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
            return;

        queue.Writer.TryComplete();
        cts.Cancel();
        try
        {
            await worker;
        }
        catch (OperationCanceledException)
        {
        }
        cts.Dispose();
    }

    // hosts may dispose the DI container synchronously
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
            return;

        queue.Writer.TryComplete();
        cts.Cancel();
        try
        {
            worker.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }
        cts.Dispose();
    }
}
