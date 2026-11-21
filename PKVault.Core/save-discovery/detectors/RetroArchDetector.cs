using System.IO.Abstractions;
using System.Text.Json;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// RetroArch (best-effort). retroarch.cfg:
/// - savefile_directory ("default" = [base]/saves, ":" prefix = relative to RetroArch folder);
/// - savefiles_in_content_dir = "true": saves beside content, folders from
///   rgui_browser_directory and content history playlist.
/// </summary>
public class RetroArchDetector(IFileSystem fileSystem, IDiscoveryEnvironment environment)
    : EmulatorDetectorBase(fileSystem, environment)
{
    public override string Id => "retroarch";
    public override string Name => "RetroArch";

    public override IEnumerable<string> GetDefaultLocations() => Environment.Os switch
    {
        DiscoveryOs.Windows => NonNull(
            Join(Environment.AppDataFolder, "RetroArch"),
            "C:/RetroArch-Win64",
            "C:/RetroArch"
        ),
        DiscoveryOs.MacOS => NonNull(
            Join(Home, "Library/Application Support/RetroArch")
        ),
        _ => NonNull(
            Join(Environment.XdgConfigHome, "retroarch"),
            Join(Home, ".var/app/org.libretro.RetroArch/config/retroarch")
        ),
    };

    public override DetectedEmulator? DetectAt(string folder)
    {
        var configPath = Join(folder, "retroarch.cfg");
        var config = ReadConfig(configPath);
        if (config == null)
            return null;

        var portable = AnyFileMatching(folder, "retroarch*.exe");

        List<EmulatorSaveFolder> folders = [];

        var saveDirectory = config.Get("savefile_directory");
        var resolvedSaveDirectory = ResolveRetroArchPath(saveDirectory, folder);
        folders.Add(resolvedSaveDirectory != null
            ? new(resolvedSaveDirectory, EmulatorSaveFolderOrigin.CONFIG_SAVE_PATH)
            : new(Join(folder, "saves")!, EmulatorSaveFolderOrigin.DEFAULT_SAVE_PATH));

        var inContentDir = string.Equals(config.Get("savefiles_in_content_dir"), "true", StringComparison.OrdinalIgnoreCase);
        if (inContentDir)
        {
            var browserDirectory = ResolveRetroArchPath(config.Get("rgui_browser_directory"), folder);
            if (browserDirectory != null)
                folders.Add(new(browserDirectory, EmulatorSaveFolderOrigin.ROM_FOLDER));

            var historyPath = ResolveRetroArchPath(config.Get("content_history_path"), folder);
            string?[] historyPaths = historyPath != null
                ? [historyPath]
                : [Join(folder, "content_history.lpl"), Join(folder, "playlists/builtin/content_history.lpl")];

            foreach (var path in historyPaths.OfType<string>().Where(FileExists))
                foreach (var romFolder in ReadPlaylistFolders(path))
                    folders.Add(new(romFolder, EmulatorSaveFolderOrigin.ROM_FOLDER));
        }

        return new(Id, Name, folder, configPath, portable, Distinct(folders, Environment.IgnoreCase));
    }

    private string? ResolveRetroArchPath(string? value, string folder)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("default", StringComparison.OrdinalIgnoreCase))
            return null;

        if (value.StartsWith(':'))
            return Join(folder, value[1..].TrimStart('/', '\\'));

        return Resolve(value, folder);
    }

    private IEnumerable<string> ReadPlaylistFolders(string playlistPath)
    {
        List<string> folders = [];
        try
        {
            using var document = JsonDocument.Parse(FileSystem.File.ReadAllText(playlistPath));
            if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return folders;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("path", out var pathElement)
                    && pathElement.ValueKind == JsonValueKind.String
                    && RomFolder(pathElement.GetString()) is string romFolder)
                {
                    folders.Add(romFolder);
                }
            }
        }
        catch
        {
            // invalid or legacy (non-JSON) playlist
        }
        return folders;
    }
}
