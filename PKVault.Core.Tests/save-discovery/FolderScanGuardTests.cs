using PKVault.Core.SaveDiscovery;

public class FolderScanGuardTests
{
    private static readonly FolderScanGuard WindowsGuard = new(new StaticDiscoveryEnvironment(
        DiscoveryOs.Windows,
        HomeFolder: "C:/Users/Ash",
        AppDataFolder: "C:/Users/Ash/AppData/Roaming",
        LocalAppDataFolder: "C:/Users/Ash/AppData/Local"
    ));

    private static readonly FolderScanGuard LinuxGuard = new(new StaticDiscoveryEnvironment(
        DiscoveryOs.Linux,
        HomeFolder: "/home/ash",
        XdgConfigHome: "/home/ash/.config",
        XdgDataHome: "/home/ash/.local/share"
    ));

    private static readonly FolderScanGuard MacGuard = new(new StaticDiscoveryEnvironment(
        DiscoveryOs.MacOS,
        HomeFolder: "/Users/ash"
    ));

    [Theory]
    [InlineData("C:\\")]
    [InlineData("C:/")]
    [InlineData("D:")]
    [InlineData("d:\\")]
    [InlineData("C:/Emulation/..")]
    public void Windows_DriveRoots_AreRefused(string path)
    {
        Assert.Equal(FolderScanRefusal.DRIVE_ROOT, WindowsGuard.Check(path).Refusal);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/home/..")]
    public void Linux_Root_IsRefused(string path)
    {
        Assert.Equal(FolderScanRefusal.DRIVE_ROOT, LinuxGuard.Check(path).Refusal);
    }

    [Theory]
    [InlineData("C:/Windows")]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("c:/windows/system32/drivers")]
    [InlineData("C:/Program Files")]
    [InlineData("C:/Program Files (x86)")]
    [InlineData("C:/ProgramData")]
    [InlineData("D:/$Recycle.Bin/S-1-5")]
    [InlineData("C:/Users/Ash/AppData")]
    [InlineData("C:/Users/Ash/AppData/Roaming")]
    [InlineData("C:/Users/Ash/AppData/Local")]
    public void Windows_SystemFolders_AreRefused(string path)
    {
        Assert.Equal(FolderScanRefusal.SYSTEM_FOLDER, WindowsGuard.Check(path).Refusal);
    }

    [Theory]
    [InlineData("/usr")]
    [InlineData("/usr/share/games")]
    [InlineData("/etc")]
    [InlineData("/bin")]
    [InlineData("/proc/1")]
    [InlineData("/opt")]
    [InlineData("/home/ash/.config")]
    [InlineData("/home/ash/.local/share")]
    public void Linux_SystemFolders_AreRefused(string path)
    {
        Assert.Equal(FolderScanRefusal.SYSTEM_FOLDER, LinuxGuard.Check(path).Refusal);
    }

    [Theory]
    [InlineData("/System")]
    [InlineData("/System/Library/CoreServices")]
    [InlineData("/Library")]
    [InlineData("/Users/ash/Library")]
    [InlineData("/Users/ash/Library/Application Support")]
    public void Mac_SystemFolders_AreRefused(string path)
    {
        Assert.Equal(FolderScanRefusal.SYSTEM_FOLDER, MacGuard.Check(path).Refusal);
    }

    [Theory]
    [InlineData("C:/Users/Ash")]
    [InlineData("C:\\Users\\ash\\")]
    [InlineData("C:/Users")]
    public void Windows_HomeAndAncestors_AreRefused(string path)
    {
        Assert.Equal(FolderScanRefusal.HOME_FOLDER, WindowsGuard.Check(path).Refusal);
    }

    [Theory]
    [InlineData("/home/ash")]
    [InlineData("/home/ash/")]
    [InlineData("/home")]
    public void Linux_HomeAndAncestors_AreRefused(string path)
    {
        Assert.Equal(FolderScanRefusal.HOME_FOLDER, LinuxGuard.Check(path).Refusal);
    }

    [Fact]
    public void Linux_IsCaseSensitive()
    {
        // "/Home/ash" is not the home folder on Linux
        Assert.Null(LinuxGuard.Check("/Home/ash").Refusal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("relative/folder")]
    [InlineData("C:relative")]
    public void InvalidPaths_AreRefused(string? path)
    {
        Assert.Equal(FolderScanRefusal.INVALID_PATH, WindowsGuard.Check(path).Refusal);
    }

    [Theory]
    [InlineData("D:/Emulation", "D:/Emulation")]
    [InlineData("D:\\Emulation\\Roms\\GBA\\", "D:/Emulation/Roms/GBA")]
    [InlineData("C:/Users/Ash/Documents/Saves", "C:/Users/Ash/Documents/Saves")]
    [InlineData("C:/Users/Ash/AppData/Roaming/mGBA", "C:/Users/Ash/AppData/Roaming/mGBA")]
    [InlineData("C:/Program Files/mGBA", "C:/Program Files/mGBA")]
    [InlineData("C:/Windows/../Games", "C:/Games")]
    public void Windows_RegularFolders_AreAllowed(string path, string expected)
    {
        var (normalized, refusal) = WindowsGuard.Check(path);
        Assert.Null(refusal);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("/home/ash/Games")]
    [InlineData("/home/ash/.config/mgba")]
    [InlineData("/run/media/ash/SDCARD/Emulation")]
    [InlineData("/media/ash/usb")]
    public void Linux_RegularFolders_AreAllowed(string path)
    {
        Assert.Null(LinuxGuard.Check(path).Refusal);
    }
}
