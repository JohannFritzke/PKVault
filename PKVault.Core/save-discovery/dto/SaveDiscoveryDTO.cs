using PKHeX.Core;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// Result of an automatic discovery (emulator detectors) or of a manual folder scan.
/// </summary>
public record SaveDiscoveryResultDTO(
    DiscoveredSaveDTO[] Saves,
    DetectedEmulatorDTO[] Emulators,
    ScannedFolderDTO[] Folders,
    // true if at least one folder hit the file cap
    bool Truncated,
    // only for a manual folder scan: why the folder was refused (null when scanned)
    FolderScanRefusal? Refusal = null
);

/// <summary>
/// A file recognized by PKHeX as a save.
/// </summary>
public record DiscoveredSaveDTO(
    string Path,
    // same identity as SaveWrapper.Id / SaveInfosDTO.Id
    uint SaveId,
    GameVersion Version,
    byte Generation,
    string TrainerName,
    uint TID,
    uint? SID,
    string PlayTime,
    int PlayTimeInSeconds,
    long FileSize,
    DateTime LastWriteTime,
    // human-readable origin, ex. "mGBA" or "Folder scan"
    string Source,
    // detector id (mgba, melonds, desmume, citra, retroarch), null for a manual folder scan
    string? EmulatorId,
    // path is already matched by current SAVE_GLOBS (or already loaded)
    bool AlreadyAdded,
    // path of the already loaded save having the same identity (SaveId), if any
    string? IsDuplicateOf
);

public record DetectedEmulatorDTO(
    string EmulatorId,
    string Name,
    string InstallPath,
    string? ConfigPath,
    bool Portable,
    EmulatorSaveFolderDTO[] SaveFolders
);

public record EmulatorSaveFolderDTO(
    string Path,
    EmulatorSaveFolderOrigin Origin
);

public record ScannedFolderDTO(
    string Path,
    string Source,
    string? EmulatorId,
    FolderScanRefusal? Refusal,
    bool Truncated,
    int FilesVisited,
    int CandidatesCount
);

public record ScanFolderRequestDTO(string Path);

public record AddDiscoveredSavesRequestDTO(string[] Paths);

public enum FolderScanRefusal
{
    INVALID_PATH,
    NOT_FOUND,
    DRIVE_ROOT,
    SYSTEM_FOLDER,
    HOME_FOLDER,
}

public enum EmulatorSaveFolderOrigin
{
    // explicit save folder from emulator config
    CONFIG_SAVE_PATH,
    // default save folder of the emulator (no explicit config)
    DEFAULT_SAVE_PATH,
    // saves are written beside the ROM: folder remembered by the emulator (last dir, recent files, library)
    ROM_FOLDER,
}
