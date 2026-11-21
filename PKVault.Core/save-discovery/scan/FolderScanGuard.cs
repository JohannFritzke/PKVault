namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// Protection rules for folder scans: refuse drive roots, OS/system folders,
/// app-data roots and the user's whole home folder (or any of its ancestors).
/// Purely lexical: existence is checked elsewhere.
/// </summary>
public class FolderScanGuard(IDiscoveryEnvironment environment)
{
    // refused with all their descendants, relative to a drive root (Windows)
    private static readonly string[] WindowsForbiddenTrees = [
        "Windows", "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs", "$WinREAgent", "Config.Msi",
    ];

    // refused only as the exact folder, relative to a drive root (Windows)
    private static readonly string[] WindowsForbiddenExact = [
        "Program Files", "Program Files (x86)", "ProgramData", "Users", "Users/Public",
    ];

    // refused only as the exact folder, relative to home (Windows)
    private static readonly string[] WindowsHomeForbiddenExact = [
        "AppData", "AppData/Roaming", "AppData/Local", "AppData/LocalLow", "AppData/Local/Temp",
    ];

    private static readonly string[] LinuxForbiddenTrees = [
        "/usr", "/etc", "/bin", "/sbin", "/lib", "/lib32", "/lib64", "/libx32", "/boot",
        "/dev", "/proc", "/sys", "/var", "/snap", "/lost+found",
    ];

    // "/run/media/<user>/<disk>" is a common removable drive mount point, so "/run" is not a forbidden tree
    private static readonly string[] LinuxForbiddenExact = [
        "/home", "/root", "/opt", "/mnt", "/media", "/srv", "/tmp", "/run", "/run/media",
    ];

    private static readonly string[] LinuxHomeForbiddenExact = [
        ".config", ".local", ".local/share", ".var", ".var/app", ".cache",
    ];

    private static readonly string[] MacForbiddenTrees = [
        "/System", "/usr", "/bin", "/sbin", "/etc", "/private", "/dev", "/cores", "/var", "/tmp",
    ];

    private static readonly string[] MacForbiddenExact = [
        "/Library", "/Applications", "/Users", "/Volumes", "/opt",
    ];

    private static readonly string[] MacHomeForbiddenExact = [
        "Library", "Library/Application Support", "Library/Preferences", "Library/Caches",
    ];

    /// <summary>
    /// Returns the normalized path and null refusal if the folder may be scanned.
    /// </summary>
    public (string? NormalizedPath, FolderScanRefusal? Refusal) Check(string? path)
    {
        var normalized = DiscoveryPath.Normalize(path);
        if (normalized == null)
            return (null, FolderScanRefusal.INVALID_PATH);

        if (DiscoveryPath.IsRoot(normalized))
            return (normalized, FolderScanRefusal.DRIVE_ROOT);

        var ignoreCase = environment.IgnoreCase;
        var home = DiscoveryPath.Normalize(environment.HomeFolder);

        // home itself, or any ancestor of home (ex. C:/Users, /home)
        if (home != null && DiscoveryPath.IsSameOrInside(home, normalized, ignoreCase))
            return (normalized, FolderScanRefusal.HOME_FOLDER);

        if (IsSystemFolder(normalized, home, ignoreCase))
            return (normalized, FolderScanRefusal.SYSTEM_FOLDER);

        return (normalized, null);
    }

    private bool IsSystemFolder(string normalized, string? home, bool ignoreCase)
    {
        bool Exact(string candidate) => DiscoveryPath.PathEquals(normalized, candidate, ignoreCase);
        bool Tree(string candidate) => DiscoveryPath.IsSameOrInside(normalized, candidate, ignoreCase);

        IEnumerable<string> HomeRelative(IEnumerable<string> relatives) => home == null
            ? []
            : relatives.Select(r => DiscoveryPath.Combine(home, r)!);

        switch (environment.Os)
        {
            case DiscoveryOs.Windows:
                {
                    var root = GetRoot(normalized);
                    if (WindowsForbiddenTrees.Any(t => Tree(root + t)))
                        return true;
                    if (WindowsForbiddenExact.Any(e => Exact(root + e)))
                        return true;
                    string?[] appDataRoots = [
                        .. HomeRelative(WindowsHomeForbiddenExact),
                        DiscoveryPath.Normalize(environment.AppDataFolder),
                        DiscoveryPath.Normalize(environment.LocalAppDataFolder),
                    ];
                    return appDataRoots.OfType<string>().Any(Exact);
                }
            case DiscoveryOs.MacOS:
                return MacForbiddenTrees.Any(Tree)
                    || MacForbiddenExact.Any(Exact)
                    || HomeRelative(MacHomeForbiddenExact).Any(Exact);
            default:
                {
                    string?[] xdgRoots = [
                        .. HomeRelative(LinuxHomeForbiddenExact),
                        DiscoveryPath.Normalize(environment.XdgConfigHome),
                        DiscoveryPath.Normalize(environment.XdgDataHome),
                    ];
                    return LinuxForbiddenTrees.Any(Tree)
                        || LinuxForbiddenExact.Any(Exact)
                        || xdgRoots.OfType<string>().Any(Exact);
                }
        }
    }

    private static string GetRoot(string normalized)
    {
        var current = normalized;
        while (DiscoveryPath.GetParent(current) is string parent)
            current = parent;
        return current.EndsWith('/') ? current : current + '/';
    }
}
