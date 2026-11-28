using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PKHeX.Core;
using PKVault.Core;
using PKVault.Core.Cloud;

public class CloudSyncTests
{
    public CloudSyncTests()
    {
        Program.Initialize();
    }

    private const string Server = "https://pb.test";

    private static readonly DateTime T10 = new(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T11 = new(2026, 11, 21, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T12 = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static PocketBaseBackupRecord Record(string id, DateTime created, string device = "NOTEBOOK", string sha = "") =>
        new(id, "col1", created, device, $"after_save_{id}.zip", null, sha);

    [Fact]
    public void Decide_EmptyServer_Pushes()
    {
        Assert.Equal(CloudSyncDecision.PUSH_LOCAL, CloudSyncService.Decide(new(), null));
    }

    [Fact]
    public void Decide_FirstSyncOfAComputer_PullsServerCopy()
    {
        Assert.Equal(CloudSyncDecision.PULL_REMOTE, CloudSyncService.Decide(new(), Record("r1", T10)));
    }

    [Fact]
    public void Decide_ServerLatestIsOurs_Nothing_OrPushWhenPending()
    {
        var config = new CloudConfig { LastBackupRecordId = "r1" };

        Assert.Equal(CloudSyncDecision.NOTHING, CloudSyncService.Decide(config, Record("r1", T10)));
        Assert.Equal(CloudSyncDecision.PUSH_LOCAL, CloudSyncService.Decide(config with { PendingLocalSaveAt = T11 }, Record("r1", T10)));
    }

    [Fact]
    public void Decide_OtherComputerSavedLater_Pulls()
    {
        var config = new CloudConfig { LastBackupRecordId = "r1" };

        Assert.Equal(CloudSyncDecision.PULL_REMOTE, CloudSyncService.Decide(config, Record("r2", T11)));
        // unsent local save, but older than the server one: newest wins
        Assert.Equal(CloudSyncDecision.PULL_REMOTE, CloudSyncService.Decide(config with { PendingLocalSaveAt = T10 }, Record("r2", T11)));
    }

    [Fact]
    public void Decide_UnsentLocalSaveNewerThanServer_Pushes()
    {
        var config = new CloudConfig { LastBackupRecordId = "r1", PendingLocalSaveAt = T12 };

        Assert.Equal(CloudSyncDecision.PUSH_LOCAL, CloudSyncService.Decide(config, Record("r2", T11)));
    }

    private static byte[] Zip(Dictionary<string, byte[]> files)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in files)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(content);
            }
        }
        return memory.ToArray();
    }

    private static byte[] Paths(string json) => Encoding.UTF8.GetBytes(json);

    private static Mock<ISavesLoadersService> SavesMock(string localSavePath)
    {
        static SaveFile Trainer()
        {
            var data = File.ReadAllBytes(Path.Combine(Program.InitialCurrentDirectory, "./assets/Pokemon - Version Saphir (France).srm"));
            SaveUtil.TryGetSaveFile(data, out var save);
            ArgumentNullException.ThrowIfNull(save);
            return save;
        }

        var localSav = Trainer();
        localSav.Metadata.SetExtraInfo(localSavePath);
        var local = new SaveWrapper(localSav);

        var mock = new Mock<ISavesLoadersService>();
        mock.Setup(m => m.GetSaveById()).Returns(new Dictionary<uint, SaveWrapper> { [ local.Id ] = local });
        mock.Setup(m => m.CheckSaveData(It.IsAny<byte[]>(), "known.sav", true)).ReturnsAsync(new SaveWrapper(Trainer()));
        mock.Setup(m => m.CheckSaveData(It.IsAny<byte[]>(), "unknown.sav", true)).ThrowsAsync(new ArgumentException("not a save"));
        return mock;
    }

    [Fact]
    public async Task BuildPlan_MapsDbToLocalDb_SavesByIdentity_AndStorageRelative()
    {
        var zip = Zip(new()
        {
            [ "_paths.json" ] = Paths("""{"db/pkvault.db":"./db/pkvault.db","saves/known.sav":"E:/other-pc/emerald.sav","saves/unknown.sav":"E:/other-pc/x.sav","main/storage/0025 - PIKACHU - 1.pk3":"./storage/0025 - PIKACHU - 1.pk3"}"""),
            [ "db/pkvault.db" ] = [ 1 ],
            [ "db/legacy.json" ] = [ 9 ],
            [ "saves/known.sav" ] = [ 2 ],
            [ "saves/unknown.sav" ] = [ 3 ],
            [ "main/storage/0025 - PIKACHU - 1.pk3" ] = [ 4 ],
        });

        var localSavePath = Path.GetTempFileName();
        try
        {
            var mock = SavesMock(localSavePath);
            var expectedPath = mock.Object.GetSaveById().Single().Value.Metadata.FilePath!;
            Assert.False(string.IsNullOrEmpty(expectedPath));

            var plan = await CloudBackupApplier.BuildPlan(zip, "C:/app/db/pkvault.db", mock.Object);

            Assert.Equal(1, plan.SavesUpdated);
            Assert.Equal(1, plan.SavesSkipped);

            var targets = plan.Files.ToDictionary(f => f.Target, f => f.Content[ 0 ]);
            Assert.Equal(3, targets.Count);
            Assert.Equal(1, targets[ "C:/app/db/pkvault.db" ]);
            Assert.Equal(2, targets[ expectedPath ]);
            Assert.Equal(4, Assert.Single(targets, t => t.Key.EndsWith("storage/0025 - PIKACHU - 1.pk3")).Value);
        }
        finally
        {
            File.Delete(localSavePath);
        }
    }

    [Fact]
    public async Task BuildPlan_AbsoluteStoragePath_IsRefused()
    {
        var zip = Zip(new()
        {
            [ "_paths.json" ] = Paths("""{"db/pkvault.db":"./db/pkvault.db","main/storage/a.pk3":"F:/MyStorage/a.pk3"}"""),
            [ "db/pkvault.db" ] = [ 1 ],
            [ "main/storage/a.pk3" ] = [ 4 ],
        });

        var ex = await Assert.ThrowsAsync<CloudSyncException>(() => CloudBackupApplier.BuildPlan(zip, "C:/app/db/pkvault.db", SavesMock("D:/x.sav").Object));
        Assert.Equal(CloudSyncError.UNSUPPORTED_PATHS, ex.Error);
    }

    [Fact]
    public async Task BuildPlan_WithoutDb_IsRefused()
    {
        var zip = Zip(new() { [ "_paths.json" ] = Paths("{}") });

        var ex = await Assert.ThrowsAsync<CloudSyncException>(() => CloudBackupApplier.BuildPlan(zip, "C:/app/db/pkvault.db", SavesMock("D:/x.sav").Object));
        Assert.Equal(CloudSyncError.INVALID_BACKUP, ex.Error);
    }

    private sealed class FakeServer(Func<HttpRequestMessage, HttpResponseMessage?> route) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return Task.FromResult(route(request) ?? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        }
    }

    private static string Jwt()
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{}")}.{B64($$"""{"exp":{{DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()}}}""")}.sig";
    }

    private static (CloudSyncService sync, CloudStore store, FakeServer server, Mock<ISessionService> session) NewSync(
        byte[] remoteZip, string remoteSha, string? lastBackupRecordId, string accountStatus = "APPROVED"
    )
    {
        var server = new FakeServer(req => (req.Method.Method, req.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/collections/users/records/u1") => Json($$"""{"id":"u1","status":"{{accountStatus}}"}"""),
            ("GET", "/api/collections/vault_backups/records") => Json(
                $$$"""{"items":[{"id":"r2","collectionId":"col1","created":"2026-11-21 11:00:00.000Z","device":"NOTEBOOK","file":"after_save_r2.zip","sha256":"{{{remoteSha}}}"}]}"""),
            ("POST", "/api/files/token") => Json("""{"token":"ft"}"""),
            ("GET", "/api/files/col1/r2/after_save_r2.zip") => new(HttpStatusCode.OK) { Content = new ByteArrayContent(remoteZip) },
            _ => null,
        });

        var store = new CloudStore(new MockFileSystem(), "C:/app/config/cloud.json");
        store.Update(c => c with { ServerUrl = Server, Token = Jwt(), UserId = "u1", LastBackupRecordId = lastBackupRecordId });

        var session = new Mock<ISessionService>();
        session.Setup(s => s.HasEmptyActionList()).Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(session.Object);
        var sp = services.BuildServiceProvider();

        var client = new PocketBaseClient(new HttpClient(server));
        var auth = new CloudAuthService(store, client, sp, TimeProvider.System) { RefreshOnStart = false };
        return (new CloudSyncService(store, auth, client, sp, TimeProvider.System), store, server, session);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Run_NewerBackupFromOtherComputer_IsDownloadedAndApplied()
    {
        byte[] zip = [ 1, 2, 3 ];
        var (sync, store, _, _) = NewSync(zip, Convert.ToHexStringLower(SHA256.HashData(zip)), lastBackupRecordId: "r1");

        byte[]? applied = null;
        sync.ApplyBackup = bytes => { applied = bytes; return Task.FromResult(new CloudBackupApplier.Result(1, 2)); };
        sync.PushSnapshot = () => throw new InvalidOperationException("must not push");

        await sync.Run();

        Assert.Equal(zip, applied);
        Assert.Equal("r2", store.Get().LastBackupRecordId);

        var status = sync.GetStatus();
        Assert.Equal(CloudSyncState.IDLE, status.State);
        Assert.Equal(1, status.Revision);
        Assert.Equal("NOTEBOOK", status.LastPulledDevice);
        Assert.Equal(2, status.LastPulledSavesSkipped);
        Assert.Equal(T11, status.LastPulledBackupAt);
    }

    [Fact]
    public async Task Run_CorruptDownload_IsNotApplied()
    {
        var (sync, store, _, _) = NewSync([ 1, 2, 3 ], new string('f', 64), lastBackupRecordId: "r1");
        sync.ApplyBackup = _ => throw new InvalidOperationException("must not apply");

        await sync.Run();

        var status = sync.GetStatus();
        Assert.Equal(CloudSyncState.ERROR, status.State);
        Assert.Equal(CloudSyncError.INVALID_BACKUP, status.Error);
        Assert.Equal("r1", store.Get().LastBackupRecordId);
    }

    [Fact]
    public async Task Run_WithUnsavedActions_Waits()
    {
        var (sync, _, server, session) = NewSync([ 1 ], "", lastBackupRecordId: "r1");
        session.Setup(s => s.HasEmptyActionList()).Returns(false);

        await sync.Run();

        Assert.Equal(CloudSyncState.WAITING_LOCAL_CHANGES, sync.GetStatus().State);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Run_InSync_DoesNothing()
    {
        var (sync, _, server, _) = NewSync([ 1 ], "", lastBackupRecordId: "r2");
        sync.ApplyBackup = _ => throw new InvalidOperationException("must not apply");
        sync.PushSnapshot = () => throw new InvalidOperationException("must not push");

        await sync.Run();

        Assert.Equal(CloudSyncState.IDLE, sync.GetStatus().State);
        Assert.Equal(0, sync.GetStatus().Revision);
        Assert.Equal([ "GET /api/collections/users/records/u1", "GET /api/collections/vault_backups/records" ], server.Requests);
    }

    [Theory]
    [InlineData("PENDING", CloudAccountStatus.PENDING)]
    [InlineData("BLOCKED", CloudAccountStatus.BLOCKED)]
    public async Task Run_AccountNotApproved_NeitherPullsNorPushes(string serverStatus, CloudAccountStatus expected)
    {
        var (sync, store, server, _) = NewSync([ 1 ], "", lastBackupRecordId: "r1", accountStatus: serverStatus);
        sync.ApplyBackup = _ => throw new InvalidOperationException("must not apply");
        sync.PushSnapshot = () => throw new InvalidOperationException("must not push");

        await sync.Run();

        Assert.Equal(CloudSyncState.WAITING_APPROVAL, sync.GetStatus().State);
        Assert.Equal(expected, store.Get().AccountStatus);
        Assert.False(store.Get().IsApproved);
        Assert.Equal([ "GET /api/collections/users/records/u1" ], server.Requests);
    }

    [Fact]
    public async Task Run_AccountApprovedLater_SyncsAgain()
    {
        var (sync, store, _, _) = NewSync([ 1 ], "", lastBackupRecordId: "r2");
        store.Update(c => c with { AccountStatus = CloudAccountStatus.PENDING });

        await sync.Run();

        Assert.Equal(CloudSyncState.IDLE, sync.GetStatus().State);
        Assert.True(store.Get().IsApproved);
    }

    [Fact]
    public async Task Run_LoggedOut_DoesNothing()
    {
        var (sync, store, server, _) = NewSync([ 1 ], "", lastBackupRecordId: "r1");
        store.Update(c => c.LoggedOut());

        await sync.Run();

        Assert.Empty(server.Requests);
    }
}
