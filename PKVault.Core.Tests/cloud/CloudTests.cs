using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.BackupProviders;
using PKVault.Core.Cloud;

public class CloudTests
{
    private const string Server = "https://pb.test";
    private const string StorePath = "C:/app/config/cloud.json";

    /// <summary>Answers PocketBase requests from a route table and records them.</summary>
    private sealed class FakePocketBase : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string? Auth, string Body)> Requests { get; } = [];
        public Func<HttpRequestMessage, string, HttpResponseMessage?> Route { get; set; } = (_, _) => null;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            request.Headers.TryGetValues("Authorization", out var auth);
            Requests.Add((request.Method, request.RequestUri!.ToString(), auth?.FirstOrDefault(), body));

            return Route(request, body) ?? new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"not found"}"""),
            };
        }

        public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static string Jwt(DateTimeOffset expiresAt)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("""{"alg":"HS256"}""")}.{B64($$"""{"exp":{{expiresAt.ToUnixTimeSeconds()}}}""")}.sig";
    }

    private static (CloudStore store, MockFileSystem fs) NewStore()
    {
        var fs = new MockFileSystem();
        return (new CloudStore(fs, StorePath), fs);
    }

    private static CloudStore LoggedInStore(string token, string? lastBackup = null)
    {
        var (store, _) = NewStore();
        store.Update(c => c with { ServerUrl = Server, Token = token, UserId = "u1", UserName = "Ash", Provider = "google", LastBackupRecordId = lastBackup });
        return store;
    }

    [Fact]
    public void Store_RoundTrip_AndLogout_KeepsServerUrl()
    {
        var (store, fs) = NewStore();
        Assert.False(store.Get().IsLoggedIn);
        Assert.Equal(CloudStore.DefaultServerUrl, store.Get().ServerUrl);

        store.Update(c => c with { ServerUrl = Server, Token = "t", UserId = "u1", UserEmail = "a@b.c", LastBackupRecordId = "r1" });

        var reread = new CloudStore(fs, StorePath).Get();
        Assert.True(reread.IsLoggedIn);
        Assert.Equal(Server, reread.ServerUrl);
        Assert.Equal("a@b.c", reread.UserEmail);
        Assert.Equal("r1", reread.LastBackupRecordId);

        store.Update(c => c.LoggedOut());
        var after = new CloudStore(fs, StorePath).Get();
        Assert.False(after.IsLoggedIn);
        Assert.Null(after.LastBackupRecordId);
        Assert.Equal(Server, after.ServerUrl);
    }

    [Fact]
    public void Store_CorruptFile_StartsLoggedOut()
    {
        var (_, fs) = NewStore();
        fs.AddFile(StorePath, new MockFileData("{not json"));

        Assert.False(new CloudStore(fs, StorePath).Get().IsLoggedIn);
    }

    [Fact]
    public void BuildAuthUrl_AddsRedirect_AndReplacesState()
    {
        var provider = new PocketBaseOAuthProvider("google",
            "https://accounts.google.com/o/oauth2/auth?client_id=abc&state=xyz&response_type=code&redirect_uri=", "verifier");

        var url = PocketBaseClient.BuildAuthUrl(provider, PocketBaseClient.RedirectUrl(Server), "client-1");

        Assert.Equal(
            "https://accounts.google.com/o/oauth2/auth?client_id=abc&state=client-1&response_type=code&redirect_uri="
            + Uri.EscapeDataString("https://pb.test/api/oauth2-redirect"),
            url);
    }

    [Fact]
    public void GetTokenExpiration_ReadsExp_AndToleratesGarbage()
    {
        var exp = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(exp, CloudAuthService.GetTokenExpiration(Jwt(exp)));
        Assert.Null(CloudAuthService.GetTokenExpiration("not-a-jwt"));
        Assert.Null(CloudAuthService.GetTokenExpiration("a.%%%.c"));
    }

    [Fact]
    public async Task Realtime_ParsesEvents()
    {
        var sse = "id:c1\nevent:PB_CONNECT\ndata:{\"clientId\":\"c1\"}\n\n: ping\n\nevent:@oauth2\ndata:{\"state\":\"c1\",\ndata:\"code\":\"abc\"}\n\n";
        using var realtime = new PocketBaseRealtime(new HttpResponseMessage(), new MemoryStream(Encoding.UTF8.GetBytes(sse)));

        var connect = await realtime.ReadEvent(CancellationToken.None);
        Assert.Equal("PB_CONNECT", connect!.Event);

        var result = await realtime.WaitForOAuthResult(CancellationToken.None);
        Assert.Equal("c1", result.State);
        Assert.Equal("abc", result.Code);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Login_FullFlow_SavesAccount()
    {
        var handler = new FakePocketBase();
        var token = Jwt(DateTimeOffset.UtcNow.AddDays(7));
        handler.Route = (req, body) => (req.Method.Method, req.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/collections/users/auth-methods") => FakePocketBase.Json(
                """{"oauth2":{"enabled":true,"providers":[{"name":"github","authURL":"https://github.com/login/oauth/authorize?client_id=x&state=s&redirect_uri=","codeVerifier":"v1"}]}}"""),
            ("GET", "/api/realtime") => new(HttpStatusCode.OK)
            {
                Content = new StringContent("event:PB_CONNECT\ndata:{\"clientId\":\"rc1\"}\n\nevent:@oauth2\ndata:{\"state\":\"rc1\",\"code\":\"the-code\"}\n\n"),
            },
            ("POST", "/api/realtime") when body.Contains("\"rc1\"") && body.Contains("@oauth2") => new(HttpStatusCode.NoContent),
            ("POST", "/api/collections/users/auth-with-oauth2") when body.Contains("\"the-code\"") && body.Contains("\"v1\"") => FakePocketBase.Json(
                $$$"""{"token":"{{{token}}}","record":{"id":"u9","email":"ash@test","name":""},"meta":{"name":"Ash K"}}"""),
            _ => null,
        };

        var (store, _) = NewStore();
        store.Update(c => c with { ServerUrl = Server });
        using var auth = new CloudAuthService(store, new PocketBaseClient(new HttpClient(handler)), new ServiceCollection().BuildServiceProvider(), TimeProvider.System);
        string? opened = null;
        auth.OpenBrowser = url => { opened = url; return true; };

        var start = await auth.StartLogin("github");

        Assert.True(start.BrowserOpened);
        Assert.Equal(start.AuthUrl, opened);
        Assert.Contains("state=rc1", start.AuthUrl);
        Assert.EndsWith(Uri.EscapeDataString("https://pb.test/api/oauth2-redirect"), start.AuthUrl);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!auth.IsLoggedIn && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        var config = store.Get();
        Assert.True(config.IsLoggedIn);
        Assert.Equal("u9", config.UserId);
        Assert.Equal("Ash K", config.UserName);
        Assert.Equal("ash@test", config.UserEmail);
        Assert.Equal("github", config.Provider);

        var status = auth.GetStatus(null);
        Assert.Equal(CloudLoginState.IDLE, status.LoginState);
        Assert.True(status.LoggedIn);
    }

    [Fact]
    public async Task Login_DeniedOnProviderPage_ReportsError()
    {
        var handler = new FakePocketBase();
        handler.Route = (req, _) => (req.Method.Method, req.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/collections/users/auth-methods") => FakePocketBase.Json(
                """{"oauth2":{"providers":[{"name":"google","authURL":"https://g/auth?state=s&redirect_uri=","codeVerifier":"v"}]}}"""),
            ("GET", "/api/realtime") => new(HttpStatusCode.OK)
            {
                Content = new StringContent("event:PB_CONNECT\ndata:{\"clientId\":\"rc\"}\n\nevent:@oauth2\ndata:{\"state\":\"rc\",\"error\":\"access_denied\"}\n\n"),
            },
            ("POST", "/api/realtime") => new(HttpStatusCode.NoContent),
            _ => null,
        };

        var (store, _) = NewStore();
        store.Update(c => c with { ServerUrl = Server });
        using var auth = new CloudAuthService(store, new PocketBaseClient(new HttpClient(handler)), new ServiceCollection().BuildServiceProvider(), TimeProvider.System);
        auth.OpenBrowser = _ => false;

        var start = await auth.StartLogin("google");
        Assert.False(start.BrowserOpened);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (auth.GetStatus(null).LoginState == CloudLoginState.WAITING_BROWSER && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        var status = auth.GetStatus(null);
        Assert.Equal(CloudLoginState.ERROR, status.LoginState);
        Assert.Equal(CloudLoginError.DENIED, status.LoginError);
        Assert.False(status.LoggedIn);
    }

    [Fact]
    public async Task Login_UnknownProvider_Throws()
    {
        var (store, _) = NewStore();
        using var auth = new CloudAuthService(store, new PocketBaseClient(new HttpClient(new FakePocketBase())), new ServiceCollection().BuildServiceProvider(), TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() => auth.StartLogin("facebook"));
    }

    [Fact]
    public async Task GetValidToken_RefreshesSoonExpiredToken_AndLogsOutWhenRefused()
    {
        var oldToken = Jwt(DateTimeOffset.UtcNow.AddHours(5));
        var newToken = Jwt(DateTimeOffset.UtcNow.AddDays(7));
        var refuse = false;

        var handler = new FakePocketBase();
        handler.Route = (req, _) => req.RequestUri!.AbsolutePath == "/api/collections/users/auth-refresh"
            ? refuse
                ? FakePocketBase.Json("""{"message":"invalid token"}""", HttpStatusCode.Unauthorized)
                : FakePocketBase.Json($$$"""{"token":"{{{newToken}}}","record":{"id":"u1","email":"a@b"}}""")
            : null;

        var store = LoggedInStore(oldToken);
        using var auth = new CloudAuthService(store, new PocketBaseClient(new HttpClient(handler)), new ServiceCollection().BuildServiceProvider(), TimeProvider.System);

        Assert.Equal(newToken, await auth.GetValidToken(CancellationToken.None));
        Assert.Equal(oldToken, handler.Requests.Single().Auth);
        Assert.Equal("Ash", store.Get().UserName);     // kept when the server sends no name

        // fresh token: no request
        Assert.Equal(newToken, await auth.GetValidToken(CancellationToken.None));
        Assert.Single(handler.Requests);

        store.Update(c => c with { Token = oldToken });
        refuse = true;
        Assert.Null(await auth.GetValidToken(CancellationToken.None));
        Assert.False(store.Get().IsLoggedIn);
        Assert.Equal(CloudLoginError.EXPIRED, auth.GetStatus(null).LoginError);
    }

    private static (PocketBaseBackupProvider provider, FakePocketBase handler, CloudStore store) NewProvider(string? lastBackup, bool shaExists, bool parentExists = true)
    {
        var token = Jwt(DateTimeOffset.UtcNow.AddDays(7));
        var handler = new FakePocketBase();
        handler.Route = (req, _) => (req.Method.Method, req.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/api/collections/vault_backups/records") => FakePocketBase.Json(shaExists
                ? """{"items":[{"id":"existing1"}]}"""
                : """{"items":[]}"""),
            ("GET", "/api/collections/vault_backups/records/r-parent") when parentExists => FakePocketBase.Json("""{"id":"r-parent"}"""),
            ("POST", "/api/collections/vault_backups/records") => FakePocketBase.Json("""{"id":"new1"}"""),
            _ => null,
        };

        var store = LoggedInStore(token, lastBackup);
        var client = new PocketBaseClient(new HttpClient(handler));
        var auth = new CloudAuthService(store, client, new ServiceCollection().BuildServiceProvider(), TimeProvider.System);
        return (new PocketBaseBackupProvider(store, auth, client) { Device = "PC-TEST", AppVersion = "9.9.9" }, handler, store);
    }

    private static readonly BackupArtifact artifact = new(
        Name: "after_save", CreatedAt: new DateTime(2026, 11, 21, 18, 30, 0, DateTimeKind.Utc),
        Content: [ 0x50, 0x4B, 0x05, 0x06 ], Sha256: new string('b', 64)
    );

    [Fact]
    public async Task Provider_UploadsWithParent_AndRemembersRecord()
    {
        var (provider, handler, store) = NewProvider(lastBackup: "r-parent", shaExists: false);

        Assert.True(provider.IsEnabled());
        await provider.Push(artifact, CancellationToken.None);

        var upload = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Contains("name=owner", upload.Body);
        Assert.Contains("u1", upload.Body);
        Assert.Contains("PC-TEST", upload.Body);
        Assert.Contains("9.9.9", upload.Body);
        Assert.Contains("r-parent", upload.Body);
        Assert.Contains("after_save_20261121T183000.zip", upload.Body);
        Assert.Contains(new string('b', 64), upload.Body);
        Assert.Equal("new1", store.Get().LastBackupRecordId);
    }

    [Fact]
    public async Task Provider_SameSha_DoesNotUploadAgain()
    {
        var (provider, handler, store) = NewProvider(lastBackup: null, shaExists: true);

        await provider.Push(artifact, CancellationToken.None);

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Contains(Uri.EscapeDataString($"sha256='{new string('b', 64)}'"), handler.Requests[ 0 ].Url);
        Assert.Equal("existing1", store.Get().LastBackupRecordId);
    }

    [Fact]
    public async Task Provider_ParentDeletedOnServer_UploadsWithoutParent()
    {
        var (provider, handler, _) = NewProvider(lastBackup: "r-parent", shaExists: false, parentExists: false);

        await provider.Push(artifact, CancellationToken.None);

        var upload = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.DoesNotContain("name=parent", upload.Body);
    }

    [Fact]
    public async Task Provider_ServerError_Throws_WithServerMessage()
    {
        var (provider, handler, _) = NewProvider(lastBackup: null, shaExists: false);
        handler.Route = (_, _) => FakePocketBase.Json("""{"message":"Failed to create record.","data":{"file":{"message":"too big"}}}""", HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<PocketBaseException>(() => provider.Push(artifact, CancellationToken.None));
        Assert.Contains("too big", ex.Message);
    }

    [Fact]
    public void Provider_LoggedOut_IsDisabled()
    {
        var (store, _) = NewStore();
        var client = new PocketBaseClient(new HttpClient(new FakePocketBase()));
        using var auth = new CloudAuthService(store, client, new ServiceCollection().BuildServiceProvider(), TimeProvider.System);

        Assert.False(new PocketBaseBackupProvider(store, auth, client).IsEnabled());
    }
}
