using System.IO.Abstractions;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// DeSmuME (best-effort).
/// Windows: desmume.ini beside the exe, [PathSettings] Battery= (default ".\Battery") holds .dsv saves.
/// Linux/macOS: config folder itself is scanned (saves layout varies between frontends).
/// </summary>
public class DesmumeDetector(IFileSystem fileSystem, IDiscoveryEnvironment environment)
    : EmulatorDetectorBase(fileSystem, environment)
{
    public override string Id => "desmume";
    public override string Name => "DeSmuME";

    public override IEnumerable<string> GetDefaultLocations() => Environment.Os switch
    {
        DiscoveryOs.Windows => NonNull(
            Join(Environment.AppDataFolder, "DeSmuME")
        ),
        DiscoveryOs.MacOS => NonNull(
            Join(Home, "Library/Application Support/DeSmuME")
        ),
        _ => NonNull(
            Join(Environment.XdgConfigHome, "desmume"),
            Join(Home, ".var/app/org.desmume.DeSmuME/config/desmume")
        ),
    };

    public override DetectedEmulator? DetectAt(string folder)
    {
        var iniPath = Join(folder, "desmume.ini");
        var config = ReadConfig(iniPath);

        if (config != null)
        {
            List<EmulatorSaveFolder> folders = [];

            var battery = config.Get("Battery", "PathSettings");
            var batteryPath = Resolve(string.IsNullOrWhiteSpace(battery) ? "Battery" : battery, folder);
            if (batteryPath != null)
                folders.Add(new(batteryPath, string.IsNullOrWhiteSpace(battery)
                    ? EmulatorSaveFolderOrigin.DEFAULT_SAVE_PATH
                    : EmulatorSaveFolderOrigin.CONFIG_SAVE_PATH));

            return new(Id, Name, folder, iniPath, Portable: true, Distinct(folders, Environment.IgnoreCase));
        }

        // config folders of GTK / macOS frontends
        var cfgPath = new[] { Join(folder, "config.cfg"), Join(folder, "desmume.cfg") }.FirstOrDefault(FileExists);
        var isDefaultFolder = GetDefaultLocations().Any(l => DiscoveryPath.PathEquals(l, folder, Environment.IgnoreCase));
        if (cfgPath == null && !isDefaultFolder)
            return null;

        return new(Id, Name, folder, cfgPath, Portable: false, [
            new(folder, EmulatorSaveFolderOrigin.DEFAULT_SAVE_PATH),
        ]);
    }
}
