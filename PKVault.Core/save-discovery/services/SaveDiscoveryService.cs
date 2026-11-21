using System.Collections.Concurrent;
using System.IO.Abstractions;
using Serilog;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// Finds candidate save files (emulator detectors + bounded folder scans) and validates them with PKHeX.
/// Read-only: never writes, moves or modifies any file. Adding saves only computes new SAVE_GLOBS,
/// persistence goes through the existing settings flow (see SaveDiscoveryController).
/// </summary>
public class SaveDiscoveryService(
    IFileSystem fileSystem,
    IDiscoveryEnvironment environment,
    IEnumerable<IEmulatorDetector> detectors,
    IFileIOService fileIOService,
    ISettingsService settingsService,
    ISavesLoadersService savesLoadersService
)
{
    public const string FolderScanSource = "Folder scan";

    public SaveScanOptions ScanOptions { get; set; } = new();

    private readonly FolderScanGuard guard = new(environment);
    private readonly SaveFileScanner scanner = new(fileSystem);
    private readonly SaveCandidateValidator validator = new(fileSystem);

    private bool IgnoreCase => environment.IgnoreCase;

    public FolderScanGuard Guard => guard;

    /// <summary>
    /// Runs all detectors on their default locations and on given emulator folders
    /// (an install folder, or a parent folder containing installs).
    /// </summary>
    public IReadOnlyList<DetectedEmulator> DetectEmulators(IEnumerable<string>? emulatorFolders, CancellationToken cancellationToken)
    {
        List<DetectedEmulator> results = [];
        HashSet<string> seen = new(DiscoveryPath.GetComparer(IgnoreCase));

        List<string> userFolders = [];
        foreach (var folder in (emulatorFolders ?? []).Select(DiscoveryPath.Normalize).OfType<string>())
        {
            if (!DirectoryExists(folder))
                continue;
            userFolders.Add(folder);
            // also look one level down: "D:/Emulation/Emulators" containing several installs
            if (guard.Check(folder).Refusal == null)
                userFolders.AddRange(EnumerateDirectories(folder));
        }

        foreach (var detector in detectors)
        {
            IEnumerable<string> locations = [
                .. detector.GetDefaultLocations().Select(DiscoveryPath.Normalize).OfType<string>(),
                .. userFolders,
            ];

            foreach (var location in locations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!seen.Add($"{detector.Id}|{location}") || !DirectoryExists(location))
                    continue;

                try
                {
                    var detected = detector.DetectAt(location);
                    if (detected != null)
                        results.Add(detected);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"Save discovery: detector {detector.Id} failed on {location}");
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Automatic discovery: detectors, then scan of every save folder they report.
    /// </summary>
    public SaveDiscoveryResultDTO Discover(IEnumerable<string>? emulatorFolders, CancellationToken cancellationToken)
    {
        var emulators = DetectEmulators(emulatorFolders, cancellationToken);

        List<(string Path, string Source, string? EmulatorId)> folders = [];
        HashSet<string> seen = new(DiscoveryPath.GetComparer(IgnoreCase));
        foreach (var emulator in emulators)
            foreach (var folder in emulator.SaveFolders)
                if (seen.Add(folder.Path))
                    folders.Add((folder.Path, emulator.Name, emulator.EmulatorId));

        var result = ScanFolders(folders, cancellationToken);

        return result with
        {
            Emulators = [.. emulators.Select(e => e.ToDTO())],
        };
    }

    /// <summary>
    /// Manual scan of a user-chosen folder, refused for roots, system and home folders.
    /// </summary>
    public SaveDiscoveryResultDTO ScanFolder(string? path, CancellationToken cancellationToken)
    {
        var (normalized, refusal) = guard.Check(path);
        if (refusal == null && !DirectoryExists(normalized!))
            refusal = FolderScanRefusal.NOT_FOUND;

        if (refusal != null)
        {
            return new(
                Saves: [],
                Emulators: [],
                Folders: [new(normalized ?? path ?? "", FolderScanSource, null, refusal, false, 0, 0)],
                Truncated: false,
                Refusal: refusal
            );
        }

        return ScanFolders([(normalized!, FolderScanSource, null)], cancellationToken);
    }

    private SaveDiscoveryResultDTO ScanFolders(
        IEnumerable<(string Path, string Source, string? EmulatorId)> folders,
        CancellationToken cancellationToken
    )
    {
        List<ScannedFolderDTO> scannedFolders = [];
        Dictionary<string, (SaveScanCandidate Candidate, string Source, string? EmulatorId)> candidates = new(DiscoveryPath.GetComparer(IgnoreCase));

        foreach (var (path, source, emulatorId) in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (normalized, refusal) = guard.Check(path);
            if (refusal == null && !DirectoryExists(normalized!))
                refusal = FolderScanRefusal.NOT_FOUND;

            if (refusal != null)
            {
                scannedFolders.Add(new(normalized ?? path, source, emulatorId, refusal, false, 0, 0));
                continue;
            }

            var scan = scanner.Scan(normalized!, ScanOptions, cancellationToken);
            foreach (var candidate in scan.Candidates)
                candidates.TryAdd(candidate.Path, (candidate, source, emulatorId));

            scannedFolders.Add(new(scan.Root, source, emulatorId, null, scan.Truncated, scan.FilesVisited, scan.Candidates.Count));
        }

        var saves = ValidateCandidates([.. candidates.Values], cancellationToken);

        return new(
            Saves: saves,
            Emulators: [],
            Folders: [.. scannedFolders],
            Truncated: scannedFolders.Any(f => f.Truncated)
        );
    }

    private DiscoveredSaveDTO[] ValidateCandidates(
        List<(SaveScanCandidate Candidate, string Source, string? EmulatorId)> candidates,
        CancellationToken cancellationToken
    )
    {
        if (candidates.Count == 0)
            return [];

        var known = GetKnownSaves();

        ConcurrentBag<DiscoveredSaveDTO> results = [];

        Parallel.ForEach(
            candidates,
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 4 },
            entry =>
            {
                var validated = validator.TryValidate(entry.Candidate.Path, ScanOptions.MaxFileSize);
                if (validated != null)
                    results.Add(ToDTO(validated, entry.Source, entry.EmulatorId, known));
            }
        );

        return [.. results.OrderBy(s => s.Path, DiscoveryPath.GetComparer(IgnoreCase))];
    }

    private DiscoveredSaveDTO ToDTO(ValidatedSave validated, string source, string? emulatorId, KnownSaves known)
    {
        var save = validated.Save;
        var saveId = save.Id;

        var alreadyAdded = known.Paths.Contains(validated.Path);

        string? duplicateOf = null;
        if (known.PathsById.TryGetValue(saveId, out var loadedPaths))
            duplicateOf = loadedPaths.FirstOrDefault(p => !DiscoveryPath.PathEquals(p, validated.Path, IgnoreCase));

        return new(
            Path: validated.Path,
            SaveId: saveId,
            Version: save.Version,
            Generation: save.Generation,
            TrainerName: save.OT,
            TID: save.TID,
            SID: save.SID,
            PlayTime: save.PlayTimeString,
            PlayTimeInSeconds: save.PlayTimeInSeconds,
            FileSize: validated.FileSize,
            LastWriteTime: validated.LastWriteTime,
            Source: source,
            EmulatorId: emulatorId,
            AlreadyAdded: alreadyAdded,
            IsDuplicateOf: duplicateOf
        );
    }

    private record KnownSaves(HashSet<string> Paths, Dictionary<uint, List<string>> PathsById);

    /// <summary>
    /// Paths matched by current SAVE_GLOBS + saves currently loaded (with their duplicates).
    /// </summary>
    private KnownSaves GetKnownSaves()
    {
        HashSet<string> paths = new(DiscoveryPath.GetComparer(IgnoreCase));
        Dictionary<uint, List<string>> pathsById = [];

        try
        {
            var globs = settingsService.GetSettings().SettingsMutable.SAVE_GLOBS;
            foreach (var path in fileIOService.Matcher.SearchPaths(globs))
                if (ToAbsolute(path) is string absolute)
                    paths.Add(absolute);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Save discovery: cannot resolve SAVE_GLOBS");
        }

        try
        {
            foreach (var saveInfos in savesLoadersService.GetAllSaveInfos().Values)
            {
                foreach (var info in (SaveInfosDTO[])[saveInfos, .. saveInfos.Duplicates])
                {
                    if (ToAbsolute(info.Path) is not string absolute)
                        continue;
                    paths.Add(absolute);
                    if (!pathsById.TryGetValue(info.Id, out var list))
                        pathsById[info.Id] = list = [];
                    list.Add(absolute);
                }
            }
        }
        catch (Exception ex)
        {
            // loaders not initialized yet
            Log.Debug(ex, "Save discovery: loaded saves not available");
        }

        return new(paths, pathsById);
    }

    /// <summary>
    /// Validates paths and returns the new SAVE_GLOBS (current + added) and the added paths.
    /// Throws ArgumentException if a path is not an absolute path to a save recognized by PKHeX.
    /// </summary>
    public (string[] SaveGlobs, string[] AddedPaths) PrepareAddPaths(string[]? paths)
    {
        if (paths == null || paths.Length == 0)
            throw new ArgumentException("No path given");

        var currentGlobs = settingsService.GetSettings().SettingsMutable.SAVE_GLOBS;
        var known = GetKnownSaves();

        HashSet<string> currentGlobsSet = new(
            currentGlobs.Select(g => DiscoveryPath.Normalize(g) ?? g),
            DiscoveryPath.GetComparer(IgnoreCase)
        );

        List<string> added = [];
        List<string> invalid = [];

        foreach (var rawPath in paths)
        {
            var path = DiscoveryPath.Normalize(rawPath);
            // glob chars would change the meaning of the added entry
            if (path == null || path.Contains('*') || path.StartsWith('!'))
            {
                invalid.Add(rawPath);
                continue;
            }

            if (known.Paths.Contains(path) || currentGlobsSet.Contains(path) || added.Contains(path, DiscoveryPath.GetComparer(IgnoreCase)))
                continue;

            if (validator.TryValidate(path) == null)
            {
                invalid.Add(rawPath);
                continue;
            }

            added.Add(path);
        }

        if (invalid.Count > 0)
            throw new ArgumentException($"Not a recognized save file: {string.Join(", ", invalid)}");

        return ([.. currentGlobs, .. added], [.. added]);
    }

    private static string? ToAbsolute(string path)
    {
        try
        {
            return DiscoveryPath.Normalize(Path.IsPathRooted(path) ? path : Path.GetFullPath(path));
        }
        catch
        {
            return null;
        }
    }

    private bool DirectoryExists(string path)
    {
        try
        {
            return fileSystem.Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<string> EnumerateDirectories(string folder)
    {
        try
        {
            return [.. fileSystem.Directory.EnumerateDirectories(folder).Select(DiscoveryPath.Normalize).OfType<string>()];
        }
        catch
        {
            return [];
        }
    }
}
