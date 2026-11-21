using System.IO.Abstractions;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// mGBA (Qt). Config folder contains config.ini (+ qt.ini, library.sqlite3).
/// Portable install: portable.ini beside mGBA.exe, config in the same folder.
/// config.ini [ports.qt] savegamePath: when empty, saves are written beside the ROM,
/// so ROM folders remembered by mGBA are used (lastDirectory, qt.ini [mru], library).
/// </summary>
public class MgbaDetector(IFileSystem fileSystem, IDiscoveryEnvironment environment, IMgbaLibraryReader libraryReader)
    : EmulatorDetectorBase(fileSystem, environment)
{
    public override string Id => "mgba";
    public override string Name => "mGBA";

    public override IEnumerable<string> GetDefaultLocations() => Environment.Os switch
    {
        DiscoveryOs.Windows => NonNull(
            Join(Environment.AppDataFolder, "mGBA")
        ),
        DiscoveryOs.MacOS => NonNull(
            Join(Home, "Library/Application Support/mGBA")
        ),
        _ => NonNull(
            Join(Environment.XdgConfigHome, "mgba"),
            Join(Home, ".var/app/io.mgba.mGBA/config/mgba")
        ),
    };

    public override DetectedEmulator? DetectAt(string folder)
    {
        var configPath = Join(folder, "config.ini");
        var config = ReadConfig(configPath);
        if (config == null)
            return null;

        var portable = FileExists(Join(folder, "portable.ini"));

        var looksLikeMgba = portable
            || config.HasSectionStartingWith("ports.")
            || FileExists(Join(folder, "qt.ini"))
            || AnyFileMatching(folder, "mGBA*")
            || AnyFileMatching(folder, "mgba*");
        if (!looksLikeMgba)
            return null;

        List<EmulatorSaveFolder> folders = [];

        // explicit save folder, ports.qt first then any other port (sdl...)
        var savegamePath = config.Find("savegamePath", "ports.qt")
            .Concat(config.Find("savegamePath"))
            .Select(e => e.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var resolvedSavePath = Resolve(savegamePath, folder);
        if (resolvedSavePath != null)
        {
            folders.Add(new(resolvedSavePath, EmulatorSaveFolderOrigin.CONFIG_SAVE_PATH));
        }
        else
        {
            foreach (var romFolder in GetRomFolders(folder, config))
                folders.Add(new(romFolder, EmulatorSaveFolderOrigin.ROM_FOLDER));
        }

        return new(Id, Name, folder, configPath, portable, Distinct(folders, Environment.IgnoreCase));
    }

    private IEnumerable<string> GetRomFolders(string folder, LooseConfig config)
    {
        foreach (var entry in config.Find("lastDirectory"))
        {
            var lastDirectory = Resolve(entry.Value, folder);
            if (lastDirectory != null)
                yield return lastDirectory;
        }

        var qtConfig = ReadConfig(Join(folder, "qt.ini"));
        if (qtConfig != null)
        {
            foreach (var entry in qtConfig.InSection("mru"))
            {
                var romFolder = RomFolder(Resolve(entry.Value, folder));
                if (romFolder != null)
                    yield return romFolder;
            }
        }

        var libraryPath = Join(folder, "library.sqlite3");
        if (libraryPath != null && FileExists(libraryPath))
        {
            foreach (var path in libraryReader.ReadRomPaths(libraryPath))
            {
                var normalized = DiscoveryPath.Normalize(path);
                if (normalized == null)
                    continue;

                // roots are folders (or archives), paths are ROM files
                if (DirectoryExists(normalized))
                    yield return normalized;
                else if (RomFolder(normalized) is string romFolder)
                    yield return romFolder;
            }
        }
    }
}
