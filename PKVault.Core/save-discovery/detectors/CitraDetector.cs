using System.IO.Abstractions;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// Citra and its forks (Lime3DS, Azahar) - best-effort.
/// Saves: [user dir]/sdmc/Nintendo 3DS/[id0]/[id1]/title/00040000/[titleId]/data/00000001/main
/// Portable install: "user" folder beside the exe.
/// </summary>
public class CitraDetector(IFileSystem fileSystem, IDiscoveryEnvironment environment)
    : EmulatorDetectorBase(fileSystem, environment)
{
    public override string Id => "citra";
    public override string Name => "Citra";

    private static readonly string[] WindowsFolders = ["Citra", "Lime3DS", "Azahar"];
    private static readonly string[] LinuxFolders = ["citra-emu", "lime3ds-emu", "azahar-emu"];

    public override IEnumerable<string> GetDefaultLocations() => Environment.Os switch
    {
        DiscoveryOs.Windows => NonNull([.. WindowsFolders.Select(f => Join(Environment.AppDataFolder, f))]),
        DiscoveryOs.MacOS => NonNull([.. WindowsFolders.Select(f => Join(Home, $"Library/Application Support/{f}"))]),
        _ => NonNull([
            .. LinuxFolders.Select(f => Join(Environment.XdgDataHome, f)),
            Join(Home, ".var/app/org.citra_emu.citra/data/citra-emu"),
            Join(Home, ".var/app/io.github.lime3ds.Lime3DS/data/lime3ds-emu"),
            Join(Home, ".var/app/org.azahar_emu.Azahar/data/azahar-emu"),
        ]),
    };

    public override DetectedEmulator? DetectAt(string folder)
    {
        var userFolder = new[] { folder, Join(folder, "user") }
            .FirstOrDefault(f => DirectoryExists(Join(f, "sdmc/Nintendo 3DS")));
        if (userFolder == null)
            return null;

        var portable = !DiscoveryPath.PathEquals(userFolder, folder, Environment.IgnoreCase);

        List<EmulatorSaveFolder> folders = [];
        var nintendo3ds = Join(userFolder, "sdmc/Nintendo 3DS")!;

        foreach (var id0 in EnumerateDirectories(nintendo3ds))
            foreach (var id1 in EnumerateDirectories(id0))
            {
                var titles = Join(id1, "title/00040000");
                if (DirectoryExists(titles))
                    folders.Add(new(titles!, EmulatorSaveFolderOrigin.DEFAULT_SAVE_PATH));
            }

        var configPath = Join(userFolder, "config/qt-config.ini");

        return new(Id, Name, folder, FileExists(configPath) ? configPath : null, portable, Distinct(folders, Environment.IgnoreCase));
    }

    private IEnumerable<string> EnumerateDirectories(string folder)
    {
        try
        {
            return [.. FileSystem.Directory.EnumerateDirectories(folder)
                .Select(DiscoveryPath.Normalize)
                .OfType<string>()];
        }
        catch
        {
            return [];
        }
    }
}
