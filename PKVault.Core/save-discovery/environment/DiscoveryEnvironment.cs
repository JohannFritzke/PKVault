using System.Runtime.InteropServices;

namespace PKVault.Core.SaveDiscovery;

public enum DiscoveryOs
{
    Windows,
    Linux,
    MacOS,
    Other,
}

/// <summary>
/// OS + well-known user folders, abstracted for tests.
/// All paths are normalized with DiscoveryPath (forward slashes) or null when unknown.
/// </summary>
public interface IDiscoveryEnvironment
{
    public DiscoveryOs Os { get; }
    public string? HomeFolder { get; }
    // Windows %APPDATA% (Roaming)
    public string? AppDataFolder { get; }
    // Windows %LOCALAPPDATA%
    public string? LocalAppDataFolder { get; }
    // Linux $XDG_CONFIG_HOME or ~/.config
    public string? XdgConfigHome { get; }
    // Linux $XDG_DATA_HOME or ~/.local/share
    public string? XdgDataHome { get; }

    public bool IgnoreCase => Os != DiscoveryOs.Linux && Os != DiscoveryOs.Other;
}

public class DiscoveryEnvironment : IDiscoveryEnvironment
{
    public DiscoveryOs Os { get; }
    public string? HomeFolder { get; }
    public string? AppDataFolder { get; }
    public string? LocalAppDataFolder { get; }
    public string? XdgConfigHome { get; }
    public string? XdgDataHome { get; }

    public DiscoveryEnvironment()
    {
        Os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? DiscoveryOs.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? DiscoveryOs.MacOS
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? DiscoveryOs.Linux
            : DiscoveryOs.Other;

        HomeFolder = FromSpecial(Environment.SpecialFolder.UserProfile);

        if (Os == DiscoveryOs.Windows)
        {
            AppDataFolder = FromSpecial(Environment.SpecialFolder.ApplicationData);
            LocalAppDataFolder = FromSpecial(Environment.SpecialFolder.LocalApplicationData);
        }

        XdgConfigHome = DiscoveryPath.Normalize(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"))
            ?? (HomeFolder != null ? DiscoveryPath.Combine(HomeFolder, ".config") : null);
        XdgDataHome = DiscoveryPath.Normalize(Environment.GetEnvironmentVariable("XDG_DATA_HOME"))
            ?? (HomeFolder != null ? DiscoveryPath.Combine(HomeFolder, ".local/share") : null);
    }

    private static string? FromSpecial(Environment.SpecialFolder folder)
    {
        try
        {
            return DiscoveryPath.Normalize(Environment.GetFolderPath(folder));
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Fixed values, for tests or explicit configuration.
/// </summary>
public record StaticDiscoveryEnvironment(
    DiscoveryOs Os,
    string? HomeFolder,
    string? AppDataFolder = null,
    string? LocalAppDataFolder = null,
    string? XdgConfigHome = null,
    string? XdgDataHome = null
) : IDiscoveryEnvironment;
