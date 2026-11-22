using PKVault.Core.BackupProviders;
using PKVault.Core.Cloud;

namespace PKVault.Core.cloud.routes;

/// <summary>
/// api/cloud
/// </summary>
[Route("api/[controller]")]
public class CloudController(CloudAuthService cloudAuthService, CloudSyncService cloudSyncService, BackupProviderDispatcher dispatcher)
{
    /// <summary>
    /// Also starts an automatic sync when the last one is older than CloudSyncService.Interval:
    /// the app polls this route, so sync happens at startup and regularly while it is open.
    /// </summary>
    [HttpGet("status")]
    public CloudStatusDTO Status()
    {
        cloudSyncService.Trigger();
        return cloudAuthService.GetStatus(dispatcher, cloudSyncService.GetStatus());
    }

    [HttpPost("sync")]
    public CloudStatusDTO Sync()
    {
        cloudSyncService.Trigger(force: true);
        return cloudAuthService.GetStatus(dispatcher, cloudSyncService.GetStatus());
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
        return cloudAuthService.GetStatus(dispatcher, cloudSyncService.GetStatus());
    }
}
