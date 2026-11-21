using PKVault.Core.SaveDiscovery;

namespace PKVault.Core.savediscovery.routes;

/// <summary>
/// api/save-discovery
/// </summary>
[Route("api/[controller]")]
public class SaveDiscoveryController(
    SaveDiscoveryService saveDiscoveryService,
    ISettingsService settingsService,
    ISessionService sessionService,
    DataService dataService
)
{
    // router has no request cancellation, keep scans bounded in time
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Automatic discovery with emulator detectors (default locations + optional install folders).
    /// </summary>
    [HttpGet]
    public SaveDiscoveryResultDTO Discover(string[] emulatorFolders)
    {
        using var cts = new CancellationTokenSource(ScanTimeout);
        return saveDiscoveryService.Discover(emulatorFolders, cts.Token);
    }

    /// <summary>
    /// Scan a user-chosen folder. Refused folders return an empty result with Refusal set.
    /// </summary>
    [HttpPost("scan")]
    public SaveDiscoveryResultDTO Scan(ScanFolderRequestDTO request)
    {
        using var cts = new CancellationTokenSource(ScanTimeout);
        return saveDiscoveryService.ScanFolder(request.Path, cts.Token);
    }

    /// <summary>
    /// Append given save paths to SAVE_GLOBS, using the same flow as settings edit.
    /// </summary>
    [HttpPost("add")]
    public async Task<DataDTO> Add(AddDiscoveredSavesRequestDTO request)
    {
        if (EnvUtil.DEMO_MODE)
            throw new InvalidOperationException("Settings update not allowed in demo mode");

        if (!sessionService.HasEmptyActionList())
            throw new InvalidOperationException($"Empty action list is required");

        var (saveGlobs, addedPaths) = saveDiscoveryService.PrepareAddPaths(request.Paths);

        DataUpdateFlags flags = new();

        if (addedPaths.Length > 0)
        {
            var settingsMutable = settingsService.GetSettings().SettingsMutable with
            {
                SAVE_GLOBS = saveGlobs,
            };

            var (RestartSession, ScanSaves) = settingsService.GetUpdateDiff(settingsMutable, flags);

            await settingsService.UpdateSettings(settingsMutable, RestartSession, ScanSaves, flags);
        }

        return await dataService.CreateDataFromUpdateFlags(flags);
    }
}
