using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using PKVault.Core;
using PKVault.Core.BackupProviders;

public class BackupProviderDispatcherTests
{
    private sealed class FakeProvider(string id, bool enabled = true, int failuresBeforeSuccess = 0) : IBackupProvider
    {
        public string Id => id;
        public List<BackupArtifact> Pushed { get; } = [];
        public int Attempts;
        public bool Enabled = enabled;

        public bool IsEnabled() => Enabled;

        public Task Push(BackupArtifact artifact, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts <= failuresBeforeSuccess)
                throw new HttpRequestException($"fail {Attempts}");

            Pushed.Add(artifact);
            return Task.CompletedTask;
        }
    }

    private static readonly TimeSpan[] noDelays = [ TimeSpan.Zero, TimeSpan.Zero ];

    private static BackupArtifact Artifact(string name = "backup") => new(
        FilePath: $"bkp/{name}.zip", Name: name, CreatedAt: new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc),
        Sha256: new string('a', 64), Size: 10
    );

    [Fact]
    public async Task Enqueue_PushesToEveryEnabledProvider_InOrder()
    {
        var a = new FakeProvider("a");
        var b = new FakeProvider("b");
        await using var dispatcher = new BackupProviderDispatcher([ a, b ], TimeProvider.System, noDelays);

        dispatcher.Enqueue(Artifact("one"));
        dispatcher.Enqueue(Artifact("two"));
        await dispatcher.WaitForIdle();

        Assert.Equal([ "one", "two" ], a.Pushed.Select(x => x.Name));
        Assert.Equal([ "one", "two" ], b.Pushed.Select(x => x.Name));
        Assert.All(dispatcher.GetStatus(), s => Assert.Equal(BackupProviderPushState.SUCCESS, s.State));
    }

    [Fact]
    public async Task DisabledProvider_IsSkipped_AndHasNoStatus()
    {
        var off = new FakeProvider("off", enabled: false);
        await using var dispatcher = new BackupProviderDispatcher([ off ], TimeProvider.System, noDelays);

        dispatcher.Enqueue(Artifact());
        await dispatcher.WaitForIdle();

        Assert.Empty(off.Pushed);
        Assert.Equal(0, off.Attempts);
        Assert.Empty(dispatcher.GetStatus());
    }

    [Fact]
    public async Task FailingProvider_IsRetried_ThenSucceeds()
    {
        var flaky = new FakeProvider("flaky", failuresBeforeSuccess: 2);
        await using var dispatcher = new BackupProviderDispatcher([ flaky ], TimeProvider.System, noDelays);

        dispatcher.Enqueue(Artifact());
        await dispatcher.WaitForIdle();

        Assert.Equal(3, flaky.Attempts);
        Assert.Single(flaky.Pushed);
        Assert.Equal(BackupProviderPushState.SUCCESS, dispatcher.GetStatus().Single().State);
    }

    [Fact]
    public async Task ProviderFailingEveryRetry_IsMarkedFailed_AndOthersStillRun()
    {
        var broken = new FakeProvider("broken", failuresBeforeSuccess: 99);
        var ok = new FakeProvider("ok");
        await using var dispatcher = new BackupProviderDispatcher([ broken, ok ], TimeProvider.System, noDelays);

        dispatcher.Enqueue(Artifact());
        await dispatcher.WaitForIdle();

        Assert.Equal(1 + noDelays.Length, broken.Attempts);
        Assert.Single(ok.Pushed);

        var status = dispatcher.GetStatus().ToDictionary(s => s.ProviderId);
        Assert.Equal(BackupProviderPushState.FAILED, status[ "broken" ].State);
        Assert.Equal("fail 3", status[ "broken" ].LastError);
        Assert.Equal(BackupProviderPushState.SUCCESS, status[ "ok" ].State);
    }

    [Fact]
    public async Task NoProviders_EnqueueIsANoop()
    {
        await using var dispatcher = new BackupProviderDispatcher([], TimeProvider.System, noDelays);

        Assert.False(dispatcher.HasProviders);
        dispatcher.Enqueue(Artifact());
        await dispatcher.WaitForIdle(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
    }

    [Fact]
    public async Task Hook_ComputesSha256AndQueues()
    {
        var provider = new FakeProvider("p");
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBackupProvider>(provider);
        services.AddBackupProviders();
        await using var sp = services.BuildServiceProvider();

        byte[] content = [ 1, 2, 3, 4 ];
        BackupProviderHook.NotifyCreated(sp, "bkp/x.zip", "x", new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc), content);
        await sp.GetRequiredService<BackupProviderDispatcher>().WaitForIdle();

        var pushed = Assert.Single(provider.Pushed);
        Assert.Equal("bkp/x.zip", pushed.FilePath);
        Assert.Equal(4, pushed.Size);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), pushed.Sha256);
    }

    [Fact]
    public void Hook_WithoutDispatcher_DoesNothing()
    {
        var sp = new ServiceCollection().BuildServiceProvider();

        BackupProviderHook.NotifyCreated(sp, "bkp/x.zip", "x", DateTime.UtcNow, [ 1 ]);
    }

    [Fact]
    public void ConfigureServices_RegistersDispatcher()
    {
        Program.Initialize();
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<BackupProviderDispatcher>());
    }
}
