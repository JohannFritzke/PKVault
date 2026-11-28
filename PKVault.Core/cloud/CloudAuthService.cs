using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.BackupProviders;
using Serilog;

namespace PKVault.Core.Cloud;

/// <summary>
/// Login with Google/GitHub through PocketBase, the same way the PocketBase JS SDK does it:
/// realtime connection + subscription to @oauth2, provider page opened in the system browser,
/// then /api/oauth2-redirect sends the code back through realtime and it is exchanged for a token.
/// Only the redirect url registered on Google/GitHub (the server one) is used.
/// </summary>
public sealed class CloudAuthService(
    CloudStore store, PocketBaseClient client, IServiceProvider sp, TimeProvider timeProvider
) : IDisposable
{
    public static readonly string[] Providers = [ "google", "github" ];
    public static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromDays(2);

    private readonly Lock gate = new();
    private CancellationTokenSource? loginCts;
    private CloudLoginState loginState = CloudLoginState.IDLE;
    private string? loginProvider;
    private CloudLoginError? loginError;
    private string? loginErrorDetail;

    /// <summary>Overridable for tests.</summary>
    public Func<string, bool> OpenBrowser { get; set; } = TryOpenBrowser;

    public bool IsLoggedIn => store.Get().IsLoggedIn;

    public bool IsApproved => store.Get().IsApproved;

    /// <summary>Asks the server whether the account is approved (the owner may have changed it), and remembers it.</summary>
    public async Task<CloudAccountStatus> RefreshAccountStatus(string token, CancellationToken ct)
    {
        var config = store.Get();
        var status = await client.GetAccountStatus(config.ServerUrl, token, config.UserId!, ct);
        if (status != config.AccountStatus)
        {
            store.Update(c => c.UserId == config.UserId ? c with { AccountStatus = status } : c);
            Log.Information($"Cloud: account is now {status}");
        }
        return status;
    }

    public CloudStatusDTO GetStatus(BackupProviderDispatcher? dispatcher, CloudSyncService.CloudSyncStatus? sync = null)
    {
        var config = store.Get();
        var push = dispatcher?.GetStatus().Find(s => s.ProviderId == PocketBaseBackupProvider.ProviderId);

        lock (gate)
        {
            return new(
                ServerUrl: config.ServerUrl,
                LoggedIn: config.IsLoggedIn,
                Provider: config.Provider,
                UserName: config.UserName,
                UserEmail: config.UserEmail,
                LoginState: loginState,
                LoginProvider: loginProvider,
                LoginError: loginError,
                LoginErrorDetail: loginErrorDetail,
                BackupState: config.IsLoggedIn ? push?.State : null,
                LastBackupAt: config.IsLoggedIn ? push?.LastBackupCreatedAt : null,
                BackupError: config.IsLoggedIn ? push?.LastError : null,
                SyncState: sync?.State ?? CloudSyncState.IDLE,
                LastSyncAt: sync?.LastSyncAt,
                SyncRevision: sync?.Revision ?? 0,
                LastPulledDevice: sync?.LastPulledDevice,
                LastPulledBackupAt: sync?.LastPulledBackupAt,
                LastPulledSavesSkipped: sync?.LastPulledSavesSkipped ?? 0,
                SyncError: sync?.Error,
                SyncErrorDetail: sync?.ErrorDetail,
                AccountStatus: config.IsLoggedIn ? config.AccountStatus : null
            );
        }
    }

    public async Task<CloudLoginStartDTO> StartLogin(string provider)
    {
        if (!Providers.Contains(provider))
            throw new ArgumentException($"Unknown login provider '{provider}'");

        var cts = new CancellationTokenSource(LoginTimeout);
        lock (gate)
        {
            loginCts?.Cancel();
            loginCts = cts;
            SetLogin(CloudLoginState.WAITING_BROWSER, provider, null, null);
        }

        var serverUrl = store.Get().ServerUrl;
        PocketBaseRealtime? realtime = null;
        try
        {
            var oauth = await client.GetOAuthProvider(serverUrl, provider, cts.Token);
            realtime = await client.ConnectRealtime(serverUrl, cts.Token);
            await client.Subscribe(serverUrl, realtime.ClientId, PocketBaseClient.OAuthTopic, cts.Token);

            var redirectUrl = PocketBaseClient.RedirectUrl(serverUrl);
            var authUrl = PocketBaseClient.BuildAuthUrl(oauth, redirectUrl, realtime.ClientId);

            var pending = realtime;
            realtime = null;
            _ = Task.Run(() => CompleteLogin(serverUrl, oauth, redirectUrl, pending, cts));

            return new(authUrl, OpenBrowser(authUrl));
        }
        catch (Exception ex)
        {
            realtime?.Dispose();
            FailLogin(cts, CloudLoginError.SERVER, ex.Message);
            throw;
        }
    }

    private async Task CompleteLogin(
        string serverUrl, PocketBaseOAuthProvider oauth, string redirectUrl, PocketBaseRealtime realtime, CancellationTokenSource cts
    )
    {
        using (realtime)
        {
            try
            {
                var result = await realtime.WaitForOAuthResult(cts.Token);

                if (!string.IsNullOrEmpty(result.Error) || string.IsNullOrEmpty(result.Code))
                {
                    FailLogin(cts, CloudLoginError.DENIED, result.Error);
                    return;
                }
                if (result.State != realtime.ClientId)
                {
                    FailLogin(cts, CloudLoginError.SERVER, "OAuth2 state mismatch");
                    return;
                }

                var auth = await client.AuthWithOAuth2Code(serverUrl, oauth.Name, result.Code, oauth.CodeVerifier, redirectUrl, cts.Token);

                lock (gate)
                {
                    if (loginCts != cts)
                        return;     // replaced by another login or a logout

                    store.Update(config => SaveAuth(config, auth) with { Provider = oauth.Name });
                    SetLogin(CloudLoginState.IDLE, null, null, null);
                    loginCts = null;
                }
                Log.Information($"Cloud: logged in with {oauth.Name} as {auth.UserId}");
            }
            catch (OperationCanceledException)
            {
                // timeout, or replaced by another login / logout (then FailLogin ignores it)
                FailLogin(cts, CloudLoginError.TIMEOUT, null);
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Cloud: login failed");
                FailLogin(cts, CloudLoginError.SERVER, ex.Message);
                return;
            }
            finally
            {
                cts.Dispose();
            }
        }

        // first sync right away: pulls the server copy when there is one, otherwise sends this computer's data
        sp.GetService<CloudSyncService>()?.Trigger(force: true);
    }

    public void Logout()
    {
        lock (gate)
        {
            loginCts?.Cancel();
            loginCts = null;
            SetLogin(CloudLoginState.IDLE, null, null, null);
            store.Update(config => config.LoggedOut());
        }
        Log.Information("Cloud: logged out");
    }

    /// <summary>Current token, refreshed when it expires soon. Null when logged out or the session is no longer valid.</summary>
    public async Task<string?> GetValidToken(CancellationToken ct)
    {
        var config = store.Get();
        if (!config.IsLoggedIn)
            return null;

        var expiresAt = GetTokenExpiration(config.Token!);
        if (expiresAt != null && expiresAt - timeProvider.GetUtcNow() > RefreshBefore)
            return config.Token;

        try
        {
            var auth = await client.AuthRefresh(config.ServerUrl, config.Token!, ct);
            var next = store.Update(c => c.UserId == config.UserId ? SaveAuth(c, auth) : c);
            return next.Token;
        }
        catch (PocketBaseException ex) when (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            Log.Warning($"Cloud: session refused by the server ({(int)ex.Status}), logging out");
            lock (gate)
            {
                store.Update(c => c.LoggedOut());
                SetLogin(CloudLoginState.ERROR, config.Provider, CloudLoginError.EXPIRED, null);
            }
            return null;
        }
    }

    /// <summary>`exp` claim of a JWT, without validating it (the server does).</summary>
    public static DateTimeOffset? GetTokenExpiration(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            var payload = parts[ 1 ].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static CloudConfig SaveAuth(CloudConfig config, PocketBaseAuth auth) => config with
    {
        Token = auth.Token,
        UserId = auth.UserId,
        UserName = auth.Name ?? (config.UserId == auth.UserId ? config.UserName : null),
        UserEmail = auth.Email ?? (config.UserId == auth.UserId ? config.UserEmail : null),
        AccountStatus = auth.Status,
        // the chain of backups belongs to one account
        LastBackupRecordId = config.UserId == auth.UserId ? config.LastBackupRecordId : null,
    };

    private void FailLogin(CancellationTokenSource cts, CloudLoginError error, string? detail)
    {
        lock (gate)
        {
            if (loginCts != cts)
                return;
            loginCts = null;
            SetLogin(CloudLoginState.ERROR, loginProvider, error, detail);
        }
    }

    private void SetLogin(CloudLoginState state, string? provider, CloudLoginError? error, string? detail)
    {
        loginState = state;
        loginProvider = provider;
        loginError = error;
        loginErrorDetail = detail;
    }

    private static bool TryOpenBrowser(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal) && !url.StartsWith("http://", StringComparison.Ordinal))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Cloud: could not open the system browser");
            return false;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            loginCts?.Cancel();
            loginCts = null;
        }
    }
}
