using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PKHeX.Core;
using PKVault.Core;
using PKVault.Core.Cloud;
using PKVault.Core.Trades;

public class TradeTests : IAsyncDisposable
{
    private const string Server = "https://pb.test";

    public TradeTests()
    {
        Program.Initialize();
    }

    #region fake server and storage

    /// <summary>Small in-memory copy of the server rules that matter to the client.</summary>
    private sealed class FakeTradeServer : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        /// <summary>trade_pokemon waiting for user u1: id, format, content</summary>
        public List<(string Id, string Format, byte[] Content, string? Trade)> ToClaim { get; } = [];
        public List<string> Claimed { get; } = [];
        public bool FailOffer { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add($"{request.Method} {path}");
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct));

            switch (request.Method.Method, path)
            {
                case ("GET", "/api/collections/users/records"):
                    return Json("""{"items":[{"id":"u1","name":"Ash"},{"id":"u2","name":"Brock"}]}""");

                case ("GET", "/api/collections/trades/records"):
                    return Json("""
                    {"items":[{"id":"t1","from":"u2","to":"u1","status":"COUNTERED","message":"hi","counterMessage":"",
                      "offeredSummary":{"species":25,"form":0,"nickname":"PIKA","level":12,"shiny":true,"gender":1,"otName":"ASH","tid":1,"version":3,"format":"pk3","fingerprint":"fp"},
                      "counteredSummary":null,
                      "created":"2026-11-21 10:00:00.000Z","expiresAt":"2026-11-24 10:00:00.000Z","closedAt":""}]}
                    """);

                case ("GET", "/api/collections/trade_pokemon/records"):
                    var items = string.Join(",", ToClaim.Select(p =>
                        $$"""{"id":"{{p.Id}}","collectionId":"tp","file":"{{p.Id}}.{{p.Format}}","format":"{{p.Format}}","species":25,"trade":"{{p.Trade}}"}"""));
                    return Json($$"""{"items":[{{items}}]}""");

                case ("POST", "/api/files/token"):
                    return Json("""{"token":"ft"}""");

                case ("POST", "/api/pokehome/trades"):
                    if (FailOffer)
                        return Json("""{"message":"Choose a friend to trade with."}""", HttpStatusCode.BadRequest);
                    return Json(TradeJson("t9", "OPEN"));
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/files/tp/"))
            {
                var id = path.Split('/')[ 4 ];
                var pokemon = ToClaim.First(p => p.Id == id);
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(pokemon.Content) };
            }

            if (request.Method == HttpMethod.Post && path.StartsWith("/api/pokehome/pokemon/") && path.EndsWith("/claimed"))
            {
                var id = path.Split('/')[ 4 ];
                Claimed.Add(id);
                ToClaim.RemoveAll(p => p.Id == id);
                return Json($$"""{"id":"{{id}}"}""");
            }

            if (request.Method == HttpMethod.Post && path.StartsWith("/api/pokehome/trades/"))
            {
                var parts = path.Split('/');
                var (tradeId, action) = (parts[ 4 ], parts[ 5 ]);
                if (action is "cancel" or "decline" && tradeId == "t9")
                    ToClaim.Add(("back1", "pk3", [], "t9"));     // own pokémon comes back
                return Json(TradeJson(tradeId, action == "accept" ? "DONE" : "CANCELLED"));
            }

            return Json("""{"message":"not found"}""", HttpStatusCode.NotFound);
        }

        private static string TradeJson(string id, string status) =>
            $$"""{"id":"{{id}}","from":"u1","to":"u2","status":"{{status}}","created":"2026-11-21 10:00:00.000Z","expiresAt":"2026-11-24 10:00:00.000Z"}""";

        public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class FakeLocalStorage : ITradeLocalStorage
    {
        public bool PendingActions { get; set; }
        public bool FailRemove { get; set; }
        public List<string> Removed { get; } = [];
        public List<string> Stored { get; } = [];
        public bool Discarded { get; private set; }

        public bool HasNoPendingActions() => !PendingActions;

        public Task<string?> GetFingerprint(string pkmVariantId) => Task.FromResult<string?>("fp");

        public Task<TradeOutgoing> PrepareOutgoing(string pkmVariantId) => pkmVariantId == "attached"
            ? throw new TradeException(TradeError.CANNOT_TRADE, "attached")
            : Task.FromResult(new TradeOutgoing(pkmVariantId, new([ 1, 2, 3 ], "pk3", "fp", 25, 0, "PIKA", 12, true, 1, "ASH", 1, 3)));

        public Task RemoveOutgoing(string pkmVariantId)
        {
            if (FailRemove)
                throw new IOException("disk full");
            Removed.Add(pkmVariantId);
            return Task.CompletedTask;
        }

        public Task DiscardPendingActions()
        {
            Discarded = true;
            return Task.CompletedTask;
        }

        public Task<List<TradeBlockDTO>> GetBlocked(string boxId) => Task.FromResult<List<TradeBlockDTO>>([]);

        public Task<List<string>> StoreIncoming(List<(string Id, string Format, byte[] Content)> pokemons)
        {
            Stored.AddRange(pokemons.Select(p => p.Id));
            return Task.FromResult(pokemons.Select(p => p.Id).ToList());
        }
    }

    private static string Jwt()
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{}")}.{B64($$"""{"exp":{{DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()}}}""")}.sig";
    }

    private static (TradeService trades, CloudStore store, FakeTradeServer server, FakeLocalStorage local) NewTrades(bool loggedIn = true)
    {
        var server = new FakeTradeServer();
        var store = new CloudStore(new MockFileSystem(), "C:/app/config/cloud.json");
        if (loggedIn)
            store.Update(c => c with { ServerUrl = Server, Token = Jwt(), UserId = "u1", UserName = "Ash" });

        var sp = new ServiceCollection().BuildServiceProvider();
        var client = new PocketBaseClient(new HttpClient(server));
        var auth = new CloudAuthService(store, client, sp, TimeProvider.System);
        var sync = new CloudSyncService(store, auth, client, sp, TimeProvider.System);
        var local = new FakeLocalStorage();
        return (new TradeService(store, auth, client, sync, sp) { LocalStorage = local }, store, server, local);
    }

    #endregion

    [Fact]
    public async Task Status_ListsTradesWithNamesAndFriends()
    {
        var (trades, _, _, _) = NewTrades();

        var status = await trades.GetStatus(TestContext.Current.CancellationToken);

        Assert.True(status.LoggedIn);
        Assert.Null(status.Error);
        Assert.Equal([ "u2" ], status.Friends.Select(f => f.Id));
        var trade = Assert.Single(status.Trades);
        Assert.Equal(TradeStatus.COUNTERED, trade.Status);
        Assert.Equal("Brock", trade.FromName);
        Assert.Equal("Ash", trade.ToName);
        Assert.False(trade.IsSender);
        Assert.Equal(25, trade.Offered!.Species);
        Assert.True(trade.Offered.Shiny);
        Assert.Null(trade.Countered);
        Assert.Null(trade.ClosedAt);
    }

    [Fact]
    public async Task Status_LoggedOut_IsEmpty()
    {
        var (trades, _, server, _) = NewTrades(loggedIn: false);

        var status = await trades.GetStatus(TestContext.Current.CancellationToken);

        Assert.False(status.LoggedIn);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Offer_UploadsThenRemovesFromStorage()
    {
        var (trades, _, server, local) = NewTrades();

        var result = await trades.Offer(new("v1", "u2", "quero eevee"), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.ErrorDetail);
        Assert.Equal([ "v1" ], local.Removed);
        var i = server.Requests.IndexOf("POST /api/pokehome/trades");
        Assert.Contains("quero eevee", server.Bodies[ i ]);
        Assert.Contains("name=to", server.Bodies[ i ]);
        Assert.Contains("filename=pokemon.pk3", server.Bodies[ i ]);
        Assert.Contains("TRADE", server.Bodies[ i ]);
    }

    [Fact]
    public async Task Offer_AsGift_SendsGiftKind()
    {
        var (trades, _, server, _) = NewTrades();

        var result = await trades.Offer(new("v1", "u2", null, Gift: true), TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.ErrorDetail);
        Assert.Contains("GIFT", server.Bodies[ server.Requests.IndexOf("POST /api/pokehome/trades") ]);
    }

    [Fact]
    public async Task Offer_WithUnsavedActions_IsRefused()
    {
        var (trades, _, server, local) = NewTrades();
        local.PendingActions = true;

        var result = await trades.Offer(new("v1", "u2", null), TestContext.Current.CancellationToken);

        Assert.Equal(TradeError.PENDING_CHANGES, result.Error);
        Assert.DoesNotContain("POST /api/pokehome/trades", server.Requests);
        Assert.Empty(local.Removed);
    }

    [Fact]
    public async Task Offer_NotTradable_NothingSent()
    {
        var (trades, _, server, _) = NewTrades();

        var result = await trades.Offer(new("attached", "u2", null), TestContext.Current.CancellationToken);

        Assert.Equal(TradeError.CANNOT_TRADE, result.Error);
        Assert.DoesNotContain("POST /api/pokehome/trades", server.Requests);
    }

    [Fact]
    public async Task Offer_ServerRefuses_PokemonStays()
    {
        var (trades, _, server, local) = NewTrades();
        server.FailOffer = true;

        var result = await trades.Offer(new("v1", "u2", null), TestContext.Current.CancellationToken);

        Assert.Equal(TradeError.SERVER, result.Error);
        Assert.Contains("Choose a friend", result.ErrorDetail);
        Assert.Empty(local.Removed);
    }

    [Fact]
    public async Task Offer_LocalRemoveFails_TradeUndoneAndServerCopyDropped()
    {
        var (trades, _, server, local) = NewTrades();
        local.FailRemove = true;

        var result = await trades.Offer(new("v1", "u2", null), TestContext.Current.CancellationToken);

        Assert.Equal(TradeError.CANNOT_TRADE, result.Error);
        Assert.True(local.Discarded);
        Assert.Contains("POST /api/pokehome/trades/t9/cancel", server.Requests);
        // the pokémon is still in the local storage: the server copy must not come back
        Assert.Equal([ "back1" ], server.Claimed);
        Assert.Empty(local.Stored);
    }

    [Fact]
    public async Task Accept_StoresReceivedPokemonThenMarksClaimed()
    {
        var (trades, store, server, local) = NewTrades();
        server.ToClaim.Add(("p1", "pk3", [ 9 ], "t1"));

        var result = await trades.Accept("t1", TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.ErrorDetail);
        Assert.Equal([ "p1" ], local.Stored);
        Assert.Equal([ "p1" ], server.Claimed);
        Assert.Empty(store.Get().StoredTradePokemonIds);
        Assert.Equal(1, trades.ClaimRevision);
    }

    [Fact]
    public async Task Claim_AlreadyStoredEarlier_IsNotStoredTwice()
    {
        var (trades, store, server, local) = NewTrades();
        server.ToClaim.Add(("p1", "pk3", [ 9 ], "t1"));
        // stored in a previous run, but "claimed" never reached the server
        store.Update(c => c with { StoredTradePokemonIds = [ "p1" ] });

        var count = await trades.ClaimPending(Jwt(), TestContext.Current.CancellationToken);

        Assert.Equal(0, count);
        Assert.Empty(local.Stored);
        Assert.Equal([ "p1" ], server.Claimed);
        Assert.Empty(store.Get().StoredTradePokemonIds);
    }

    [Fact]
    public async Task Claim_WithUnsavedActions_Waits()
    {
        var (trades, _, server, local) = NewTrades();
        server.ToClaim.Add(("p1", "pk3", [ 9 ], "t1"));
        local.PendingActions = true;

        var count = await trades.ClaimPending(Jwt(), TestContext.Current.CancellationToken);

        Assert.Equal(0, count);
        Assert.Empty(local.Stored);
        Assert.Empty(server.Claimed);
    }

    [Fact]
    public void CloudStore_KeepsStoredTradeIds()
    {
        var fs = new MockFileSystem();
        new CloudStore(fs, "C:/c.json").Update(c => c with { StoredTradePokemonIds = [ "a", "b" ] });

        Assert.Equal([ "a", "b" ], new CloudStore(fs, "C:/c.json").Get().StoredTradePokemonIds);
    }

    [Fact]
    public void Parse_RoundTripsPokemonBytes()
    {
        var pk = new PK3 { Species = 25, PID = 12345, TID16 = 54321, Nickname = "PIKA", CurrentLevel = 12 };
        pk.RefreshChecksum();
        var bytes = new ImmutablePKM(pk).GetDecryptedDataParty();

        var parsed = TradeLocalStorage.Parse(bytes, "pk3");

        Assert.NotNull(parsed);
        Assert.Equal(25, parsed.Species);
        Assert.Equal(12345u, parsed.GetMutablePkm().PID);
        Assert.Null(TradeLocalStorage.Parse([ 1, 2, 3 ], "pk3"));
    }

    #region TradeReceivePkmAction with a real database

    private SessionDbContext? db;

    public async ValueTask DisposeAsync()
    {
        if (db != null)
            await db.Database.EnsureDeletedAsync();
        GC.SuppressFinalize(this);
    }

    private ImmutablePKM? duplicateInSlot1;

    private async Task<(TradeReceivePkmAction action, PkmVariantLoader variants)> NewReceiveAction(int slotCount, int occupied)
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>(), Directory.GetCurrentDirectory());
        var fileIO = new FileIOService(fs);
        fileIO.Matcher.GetAllPaths = (rootDir, globs) => [ .. fs.AllFiles ];

        var session = new Mock<ISessionServiceMinimal>();
        session.Setup(s => s.SessionDbPath).Returns($"db-TradeTests-{Guid.NewGuid()}.db");
        db = new(session.Object, new Mock<DbSeedingService>(fileIO).Object);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetSettings()).Returns(new SettingsDTO(
            BuildID: default, RuntimeSystem: RuntimeSystem.LINUX, SourceProvider: SourceProvider.GithubRelease, FlatpakMigrated: false,
            Version: "", PkhexVersion: "", AppDirectory: "app", SettingsPath: "", UserId: "",
            SettingsMutable: new(
                DB_PATH: "mock-db", SAVE_GLOBS: [], PKM_EXTERNAL_GLOBS: [], STORAGE_PATH: "mock-storage", BACKUP_PATH: "mock-bkp",
                LANGUAGE: "en", HIDE_CHEATS: false, SKIP_LEGALITY_CHECKS: false
            )
        ));
        var staticData = new StaticDataService(settings.Object);
        var pkmFiles = new PkmFileLoader(fileIO, session.Object, settings.Object, db);
        var variants = new PkmVariantLoader(fileIO, session.Object, settings.Object, pkmFiles, db, staticData);

        var services = new ServiceCollection();
        services.AddScoped(_ => new Mock<IInventoryLoader>().Object);
        var boxes = new BoxLoader(session.Object, db, variants, services.BuildServiceProvider());
        var banks = new BankLoader(session.Object, db, boxes);

        await db.Banks.AddAsync(new() { Id = "1", IdInt = 1, IsDefault = true, IsExternal = false, Name = "Bank 1", Order = 0, View = new([], []) }, TestContext.Current.CancellationToken);
        await db.Boxes.AddAsync(new() { Id = "1", IdInt = 1, Name = "Box 1", Order = 0, Type = BoxType.Box, SlotCount = slotCount, BankId = "1" }, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var box = (await boxes.GetDto("1"))!;
        for (var slot = 0; slot < occupied; slot++)
        {
            var pkm = slot == 1 && duplicateInSlot1 != null ? duplicateInSlot1 : Pkm((uint)(1000 + slot));
            await variants.AddEntity(new(box, slot, true, false, null, null, EntityContext.Gen3, 3, pkm));
        }

        return (new TradeReceivePkmAction(variants, banks, boxes, new MainCreateBoxAction(boxes)), variants);
    }

    private static ImmutablePKM Pkm(uint pid)
    {
        var pk = new PK3 { Species = 133, PID = pid, TID16 = 54321 };
        pk.RefreshChecksum();
        return new ImmutablePKM(pk);
    }

    [Fact]
    public async Task Receive_GoesToFirstFreeSlot()
    {
        var (action, variants) = await NewReceiveAction(slotCount: 30, occupied: 2);
        var input = new TradeReceivePkmActionInput(Pkm(7));

        var payload = await action.ExecuteWithPayload(input, new());

        Assert.Equal(DataActionType.MAIN_CREATE_PKM_VERSION, payload.type);
        var created = await variants.GetEntity(input.CreatedVariantId!);
        Assert.NotNull(created);
        Assert.Equal("1", created.BoxId);
        Assert.Equal(2, created.BoxSlot);
        Assert.True(created.IsMain);
        Assert.Null(created.AttachedSaveId);
    }

    [Fact]
    public async Task Receive_FullBank_CreatesBox()
    {
        var (action, variants) = await NewReceiveAction(slotCount: 2, occupied: 2);
        var input = new TradeReceivePkmActionInput(Pkm(7));

        await action.ExecuteWithPayload(input, new());

        var created = await variants.GetEntity(input.CreatedVariantId!);
        Assert.NotEqual("1", created!.BoxId);
        Assert.Equal(0, created.BoxSlot);
    }

    #endregion

    [Fact]
    public async Task PrepareOutgoing_CopyElsewhereInStorage_IsRefused()
    {
        // slot 0 and slot 1 hold the same pokémon (what "detach" or a copy leaves behind)
        duplicateInSlot1 = Pkm(1000);
        var (_, variants) = await NewReceiveAction(slotCount: 30, occupied: 3);
        var local = LocalStorageOver(variants);
        var first = (await variants.GetEntitiesByBox("1", 0)).Values.Single();
        var other = (await variants.GetEntitiesByBox("1", 2)).Values.Single();

        var ex = await Assert.ThrowsAsync<TradeException>(() => local.PrepareOutgoing(first.Id));
        Assert.Equal(TradeError.DUPLICATE, ex.Error);
        Assert.Contains("slot 2", ex.Message);

        // a unique pokémon is fine
        var outgoing = await local.PrepareOutgoing(other.Id);
        Assert.Equal(other.Id, outgoing.VariantId);
        Assert.Equal("pk3", outgoing.Upload.Format);
    }

    [Fact]
    public async Task ReceivedPokemon_CanBeSentBack()
    {
        var (action, variants) = await NewReceiveAction(slotCount: 30, occupied: 2);
        var local = LocalStorageOver(variants);

        // what the sender uploads, as the receiver parses it
        var sent = await local.PrepareOutgoing((await variants.GetEntitiesByBox("1", 0)).Values.Single().Id);
        var received = TradeLocalStorage.Parse(sent.Upload.Content, sent.Upload.Format)!;
        await variants.DeleteEntity((await variants.GetEntity(sent.VariantId))!);

        var input = new TradeReceivePkmActionInput(received);
        await action.ExecuteWithPayload(input, new());

        var back = await local.PrepareOutgoing(input.CreatedVariantId!);
        Assert.Equal(sent.Upload.Fingerprint, back.Upload.Fingerprint);
    }

    private static TradeLocalStorage LocalStorageOver(PkmVariantLoader variants, Func<ImmutablePKM, bool>? isLegal = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IPkmVariantLoader>(_ => variants);
        return new TradeLocalStorage(services.BuildServiceProvider()) { IsLegal = isLegal ?? (_ => true) };
    }

    [Fact]
    public async Task PrepareOutgoing_Illegal_IsRefused_EvenWhenLegalityChecksAreSkipped()
    {
        var (_, variants) = await NewReceiveAction(slotCount: 30, occupied: 1);
        // the real PKHeX check: a made-up PK3 is not legal
        var local = LocalStorageOver(variants, TradeLegality.IsLegal);
        var first = (await variants.GetEntitiesByBox("1", 0)).Values.Single();

        var ex = await Assert.ThrowsAsync<TradeException>(() => local.PrepareOutgoing(first.Id));
        Assert.Equal(TradeError.ILLEGAL, ex.Error);
    }

    [Fact]
    public async Task GetBlocked_ListsDuplicatesAndIllegal()
    {
        duplicateInSlot1 = Pkm(1000);
        var (_, variants) = await NewReceiveAction(slotCount: 30, occupied: 3);
        var local = LocalStorageOver(variants, pkm => pkm.PID != 1002);

        var blocked = (await local.GetBlocked("1")).ToDictionary(b => b.PkmVariantId);

        var slot0 = (await variants.GetEntitiesByBox("1", 0)).Values.Single();
        var slot1 = (await variants.GetEntitiesByBox("1", 1)).Values.Single();
        var slot2 = (await variants.GetEntitiesByBox("1", 2)).Values.Single();
        Assert.Equal(TradeBlockReason.DUPLICATE, blocked[ slot0.Id ].Reason);
        Assert.Equal(TradeBlockReason.DUPLICATE, blocked[ slot1.Id ].Reason);
        Assert.Contains("slot 1", blocked[ slot1.Id ].Detail);
        Assert.Equal(TradeBlockReason.ILLEGAL, blocked[ slot2.Id ].Reason);
        Assert.Equal(3, blocked.Count);
    }
}
