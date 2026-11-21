using PKVault.Core.BackupProviders;
using PKVault.Core.Cloud;

namespace PKVault.Core.cloud.routes;

/// <summary>
/// api/cloud
/// </summary>
[Route("api/[controller]")]
public class CloudController(CloudAuthService cloudAuthService, BackupProviderDispatcher dispatcher)
{
    [HttpGet("status")]
    public CloudStatusDTO Status()
    {
        return cloudAuthService.GetStatus(dispatcher);
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
        return cloudAuthService.GetStatus(dispatcher);
    }
}
