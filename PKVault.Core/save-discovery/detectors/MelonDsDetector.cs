using System.IO.Abstractions;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// melonDS (best-effort). Config "melonDS.toml" (1.x) or "melonDS.ini" (0.9.x).
/// On Windows the config usually sits beside melonDS.exe.
/// SaveFilePath empty: saves are written beside the ROM (LastROMFolder, RecentROM).
/// </summary>
public class MelonDsDetector(IFileSystem fileSystem, IDiscoveryEnvironment environment)
    : EmulatorDetectorBase(fileSystem, environment)
{
    public override string Id => "melonds";
    public override string Name => "melonDS";

    public override IEnumerable<string> GetDefaultLocations() => Environment.Os switch
    {
        DiscoveryOs.Windows => NonNull(
            Join(Environment.AppDataFolder, "melonDS")
        ),
        DiscoveryOs.MacOS => NonNull(
            Join(Home, "Library/Preferences/melonDS"),
            Join(Home, "Library/Application Support/melonDS")
        ),
        _ => NonNull(
            Join(Environment.XdgConfigHome, "melonDS"),
            Join(Home, ".var/app/net.kuribo64.melonDS/config/melonDS")
        ),
    };

    public override DetectedEmulator? DetectAt(string folder)
    {
        var configPath = new[] { Join(folder, "melonDS.toml"), Join(folder, "melonDS.ini") }
            .FirstOrDefault(FileExists);
        var config = ReadConfig(configPath);
        if (config == null)
            return null;

        var portable = AnyFileMatching(folder, "melonDS*.exe") || AnyFileMatching(folder, "portable*");

        List<EmulatorSaveFolder> folders = [];

        var saveFilePath = config.Find("SaveFilePath")
            .Select(e => e.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var resolvedSavePath = Resolve(saveFilePath, folder);
        if (resolvedSavePath != null)
        {
            folders.Add(new(resolvedSavePath, EmulatorSaveFolderOrigin.CONFIG_SAVE_PATH));
        }
        else
        {
            foreach (var entry in config.Find("LastROMFolder"))
            {
                var lastFolder = Resolve(entry.Value, folder);
                if (lastFolder != null)
                    folders.Add(new(lastFolder, EmulatorSaveFolderOrigin.ROM_FOLDER));
            }

            var recentEntries = config.Entries.Where(e =>
                e.Key.StartsWith("RecentROM", StringComparison.OrdinalIgnoreCase)
            );
            foreach (var entry in recentEntries)
            {
                foreach (var value in entry.Values)
                {
                    var romFolder = RomFolder(Resolve(value, folder));
                    if (romFolder != null)
                        folders.Add(new(romFolder, EmulatorSaveFolderOrigin.ROM_FOLDER));
                }
            }
        }

        return new(Id, Name, folder, configPath, portable, Distinct(folders, Environment.IgnoreCase));
    }
}
