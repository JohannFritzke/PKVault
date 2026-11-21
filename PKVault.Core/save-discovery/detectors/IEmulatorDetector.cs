using System.IO.Abstractions;

namespace PKVault.Core.SaveDiscovery;

public record EmulatorSaveFolder(string Path, EmulatorSaveFolderOrigin Origin);

public record DetectedEmulator(
    string EmulatorId,
    string Name,
    string InstallPath,
    string? ConfigPath,
    bool Portable,
    IReadOnlyList<EmulatorSaveFolder> SaveFolders
)
{
    public DetectedEmulatorDTO ToDTO() => new(
        EmulatorId, Name, InstallPath, ConfigPath, Portable,
        [.. SaveFolders.Select(f => new EmulatorSaveFolderDTO(f.Path, f.Origin))]
    );
}

/// <summary>
/// One emulator: where it is installed by default, and how to read its config
/// to learn where saves are written. Implementations must only read files.
/// </summary>
public interface IEmulatorDetector
{
    public string Id { get; }
    public string Name { get; }

    /// <summary>
    /// Known config/install folders for the current OS (existence not checked).
    /// </summary>
    public IEnumerable<string> GetDefaultLocations();

    /// <summary>
    /// Detects this emulator in given folder (default location or user-given install folder).
    /// Returns null if folder is not an install/config folder of this emulator.
    /// </summary>
    public DetectedEmulator? DetectAt(string folder);
}

/// <summary>
/// Common helpers for detectors.
/// </summary>
public abstract class EmulatorDetectorBase(IFileSystem fileSystem, IDiscoveryEnvironment environment) : IEmulatorDetector
{
    protected IFileSystem FileSystem => fileSystem;
    protected IDiscoveryEnvironment Environment => environment;

    public abstract string Id { get; }
    public abstract string Name { get; }

    public abstract IEnumerable<string> GetDefaultLocations();
    public abstract DetectedEmulator? DetectAt(string folder);

    protected string? Home => environment.HomeFolder;

    protected string? Join(string? baseDir, string relative) => baseDir == null ? null : DiscoveryPath.Combine(baseDir, relative);

    protected bool FileExists(string? path)
    {
        try
        {
            return path != null && fileSystem.File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    protected bool DirectoryExists(string? path)
    {
        try
        {
            return path != null && fileSystem.Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    protected bool AnyFileMatching(string folder, string searchPattern)
    {
        try
        {
            return fileSystem.Directory.EnumerateFiles(folder, searchPattern).Any();
        }
        catch
        {
            return false;
        }
    }

    protected LooseConfig? ReadConfig(string? path)
    {
        if (!FileExists(path))
            return null;
        try
        {
            return LooseConfig.Parse(fileSystem.File.ReadAllText(path!));
        }
        catch
        {
            return null;
        }
    }

    protected string? Resolve(string? value, string? baseDir) => DiscoveryPath.Resolve(value, baseDir, Home);

    /// <summary>
    /// Folder containing a remembered ROM path. ROM may be inside an archive ("x.zip#rom.gba" or "x.zip/rom.gba").
    /// </summary>
    protected string? RomFolder(string? romPath)
    {
        if (string.IsNullOrWhiteSpace(romPath))
            return null;

        var hashIndex = romPath.IndexOf('#');
        if (hashIndex > 0)
            romPath = romPath[..hashIndex];

        var normalized = DiscoveryPath.Normalize(romPath);
        if (normalized == null)
            return null;

        // walk up until reaching a path which is not a file (handles archives seen as folders)
        var current = DiscoveryPath.GetParent(normalized);
        while (current != null && FileExists(current))
            current = DiscoveryPath.GetParent(current);
        return current;
    }

    protected static IReadOnlyList<EmulatorSaveFolder> Distinct(IEnumerable<EmulatorSaveFolder> folders, bool ignoreCase)
    {
        HashSet<string> seen = new(DiscoveryPath.GetComparer(ignoreCase));
        List<EmulatorSaveFolder> result = [];
        foreach (var folder in folders)
        {
            if (seen.Add(folder.Path))
                result.Add(folder);
        }
        return result;
    }

    protected IEnumerable<string> NonNull(params string?[] paths) => paths.OfType<string>();
}
