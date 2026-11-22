using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.BackupProviders;
using PKVault.Core.Trades;
using Serilog;

namespace PKVault.Core.Cloud;

public enum CloudSyncDecision
{
    NOTHING,
    /// <summary>This computer has the newest data: send a snapshot.</summary>
    PUSH_LOCAL,
    /// <summary>The server has newer data from another computer: replace local data.</summary>
    PULL_REMOTE,
}

/// <summary>
/// Automatic sync with the server, the newest version always wins.
/// Runs when the app asks for the cloud status (at most every <see cref="Interval"/>), after login, or on demand.
/// Never runs while there are unsaved actions or while an upload is in progress.
/// </summary>
public sealed class CloudSyncService(
    CloudStore store, CloudAuthService auth, PocketBaseClient client, IServiceProvider sp, TimeProvider timeProvider
) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim running = new(1, 1);
    private readonly Lock gate = new();
    private DateTimeOffset lastRunAt = DateTimeOffset.MinValue;

    private CloudSyncState state = CloudSyncState.IDLE;
    private DateTime? lastSyncAt;
    private string? lastPulledDevice;
    private DateTime? lastPulledBackupAt;
    private int lastPulledSavesSkipped;
    private int revision;
    private CloudSyncError? error;
    private string? errorDetail;

    /// <summary>Overridable for tests.</summary>
    public Func<byte[], Task<CloudBackupApplier.Result>> ApplyBackup { get; set; } = null!;
    public Func<Task> PushSnapshot { get; set; } = null!;
    /// <summary>Runs after each successful sync, with the sync lock held. Default: store pokémon received by trade.</summary>
    public Func<string, CancellationToken, Task>? AfterSync { get; set; }

    public static CloudSyncDecision Decide(CloudConfig config, PocketBaseBackupRecord? latest)
    {
        // empty server: give it a copy
        if (latest == null)
            return CloudSyncDecision.PUSH_LOCAL;

        if (latest.Id == config.LastBackupRecordId)
            return config.PendingLocalSaveAt != null ? CloudSyncDecision.PUSH_LOCAL : CloudSyncDecision.NOTHING;

        // a save made here that is not on the server yet, and more recent than the server one
        if (config.PendingLocalSaveAt != null && config.PendingLocalSaveAt > latest.Created)
            return CloudSyncDecision.PUSH_LOCAL;

        // includes the first sync of a computer: the server copy wins (local data goes to a local safety backup)
        return CloudSyncDecision.PULL_REMOTE;
    }

    public CloudSyncStatus GetStatus()
    {
        lock (gate)
        {
            return new(state, lastSyncAt, lastPulledDevice, lastPulledBackupAt, lastPulledSavesSkipped, revision, error, errorDetail);
        }
    }

    public record CloudSyncStatus(
        CloudSyncState State, DateTime? LastSyncAt, string? LastPulledDevice, DateTime? LastPulledBackupAt,
        int LastPulledSavesSkipped, int Revision, CloudSyncError? Error, string? ErrorDetail
    );

    /// <summary>Starts a sync in the background when the last one is old enough (or always with force).</summary>
    public void Trigger(bool force = false)
    {
        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            if (!force && now - lastRunAt < Interval)
                return;
            lastRunAt = now;
        }
        _ = Task.Run(Run);
    }

    public async Task Run()
    {
        if (!await running.WaitAsync(0))
            return;

        using var cts = new CancellationTokenSource(RunTimeout);
        try
        {
            if (!auth.IsLoggedIn)
            {
                SetState(CloudSyncState.IDLE);
                return;
            }

            var dispatcher = sp.GetService<BackupProviderDispatcher>();
            if (dispatcher != null && !dispatcher.IsIdle)
                return;     // an upload is running, check again later

            if (!HasNoPendingActions())
            {
                SetState(CloudSyncState.WAITING_LOCAL_CHANGES);
                return;
            }

            SetState(CloudSyncState.CHECKING);

            var token = await auth.GetValidToken(cts.Token);
            if (token == null)
            {
                SetState(CloudSyncState.IDLE);
                return;
            }

            var config = store.Get();
            var latest = (await client.ListBackups(config.ServerUrl, token, 1, cts.Token)).FirstOrDefault();
            var decision = Decide(config, latest);
            Log.Information($"Cloud sync: {decision} (local last={config.LastBackupRecordId ?? "none"}, pending={config.PendingLocalSaveAt?.ToString("o") ?? "none"}, server latest={latest?.Id ?? "none"} {latest?.Created:o} from {latest?.Device})");

            switch (decision)
            {
                case CloudSyncDecision.PUSH_LOCAL:
                    await (PushSnapshot?.Invoke() ?? DefaultPushSnapshot());
                    break;

                case CloudSyncDecision.PULL_REMOTE:
                    await Pull(config, latest!, token, cts.Token);
                    break;
            }

            // after a pull, so received pokémon go into the newest data
            if (AfterSync != null)
                await AfterSync(token, cts.Token);
            else if (sp.GetService<TradeService>() is TradeService trades)
                await trades.ClaimPending(token, cts.Token);

            lock (gate)
            {
                state = CloudSyncState.IDLE;
                lastSyncAt = timeProvider.GetUtcNow().UtcDateTime;
                error = null;
                errorDetail = null;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Cloud sync failed");
            lock (gate)
            {
                state = CloudSyncState.ERROR;
                error = ex is CloudSyncException syncEx ? syncEx.Error : CloudSyncError.SERVER;
                errorDetail = ex.Message;
            }
        }
        finally
        {
            running.Release();
        }
    }

    /// <summary>
    /// Runs work that changes local data and the server (trades), never at the same time as a sync.
    /// </summary>
    public async Task<T> RunExclusive<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await running.WaitAsync(ct);
        try
        {
            return await work();
        }
        finally
        {
            running.Release();
        }
    }

    private async Task Pull(CloudConfig config, PocketBaseBackupRecord latest, string token, CancellationToken ct)
    {
        SetState(CloudSyncState.PULLING);

        var zip = await client.DownloadBackup(config.ServerUrl, token, latest, ct);

        if (latest.Sha256 != "" && Convert.ToHexStringLower(SHA256.HashData(zip)) != latest.Sha256)
            throw new CloudSyncException(CloudSyncError.INVALID_BACKUP, "Downloaded backup does not match its sha256");

        // a save may have started while downloading
        if (!HasNoPendingActions())
        {
            SetState(CloudSyncState.WAITING_LOCAL_CHANGES);
            return;
        }

        var result = await (ApplyBackup?.Invoke(zip) ?? CloudBackupApplier.Apply(sp, zip, new()));

        store.Update(c => c.UserId == config.UserId
            ? c with { LastBackupRecordId = latest.Id, PendingLocalSaveAt = null }
            : c);

        lock (gate)
        {
            revision++;
            lastPulledDevice = latest.Device;
            lastPulledBackupAt = latest.Created;
            lastPulledSavesSkipped = result.SavesSkipped;
        }
        Log.Information($"Cloud sync: applied backup {latest.Id} from {latest.Device} ({result.SavesUpdated} saves updated, {result.SavesSkipped} skipped)");
    }

    private Task DefaultPushSnapshot()
    {
        var backupService = sp.GetService<BackupService>();
        return backupService == null ? Task.CompletedTask : BackupProviderHook.AfterSave(sp, backupService);
    }

    private bool HasNoPendingActions()
    {
        try
        {
            var session = sp.GetService<ISessionService>();
            return session == null || session.HasEmptyActionList();
        }
        catch (Exception)
        {
            // session not ready yet
            return false;
        }
    }

    private void SetState(CloudSyncState next)
    {
        lock (gate)
        {
            state = next;
        }
    }

    public void Dispose() => running.Dispose();
}
