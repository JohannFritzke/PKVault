using PKVault.Core.BackupProviders;
using PKVault.Core.Cloud;
using PKVault.Core.Trades;

namespace PKVault.Core.cloud.routes;

/// <summary>
/// api/cloud
/// </summary>
[Route("api/[controller]")]
public class CloudController(CloudAuthService cloudAuthService, CloudSyncService cloudSyncService, BackupProviderDispatcher dispatcher, TradeService tradeService)
{
    /// <summary>
    /// Also starts an automatic sync when the last one is older than CloudSyncService.Interval:
    /// the app polls this route, so sync happens at startup and regularly while it is open.
    /// </summary>
    [HttpGet("status")]
    public CloudStatusDTO Status()
    {
        cloudSyncService.Trigger();
        return GetStatus();
    }

    [HttpPost("sync")]
    public CloudStatusDTO Sync()
    {
        cloudSyncService.Trigger(force: true);
        return GetStatus();
    }

    /// <summary>
    /// Starts a Google/GitHub login. Opens the system browser when possible;
    /// the returned url is also shown so the user can open it by hand. Then poll status.
    /// </summary>
    [HttpPost("login")]
    public async Task<CloudLoginStartDTO> Login(CloudLoginRequestDTO request)
    {
        return await cloudAuthService.StartLogin(request.Provider);
    }

    [HttpPost("logout")]
    public CloudStatusDTO Logout()
    {
        cloudAuthService.Logout();
        return GetStatus();
    }

    private CloudStatusDTO GetStatus() =>
        cloudAuthService.GetStatus(dispatcher, cloudSyncService.GetStatus()) with { TradeClaimRevision = tradeService.ClaimRevision };
}
