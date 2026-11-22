using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PKVault.Core.Cloud;

public class CloudSyncException(CloudSyncError error, string message) : Exception(message)
{
    public CloudSyncError Error { get; } = error;
}

/// <summary>
/// Replaces local data with a backup made on another computer.
/// Unlike BackupService.RestoreBackup (same computer), file paths are mapped to this computer:
/// - db: to the local main db path,
/// - saves: by save identity (game + trainer) to the local save file with the same id; unknown saves are skipped,
/// - storage pkm files: relative paths only (default "./storage"); absolute ones are refused.
/// A local safety backup is made first and restored if anything fails.
/// </summary>
public static class CloudBackupApplier
{
    public record Result(int SavesUpdated, int SavesSkipped);

    public static async Task<Result> Apply(IServiceProvider sp, byte[] zip, DataUpdateFlags flags)
    {
        var backupService = sp.GetRequiredService<BackupService>();
        var sessionService = sp.GetRequiredService<ISessionService>();
        var savesLoadersService = sp.GetRequiredService<ISavesLoadersService>();
        var settingsService = sp.GetRequiredService<ISettingsService>();
        var fileIOService = sp.GetRequiredService<IFileIOService>();

        var plan = await BuildPlan(zip, sessionService.MainDbPath, savesLoadersService);

        using var _ = Log.Logger.Time($"Cloud sync: apply backup ({plan.Files.Count} files, {plan.SavesSkipped} saves skipped)");

        var safeBackupTime = await backupService.CreateBackup("before_cloud_sync", flags);

        try
        {
            // same cleanup as RestoreBackup: no remaining old data
            fileIOService.Delete(sessionService.MainDbPath);
            DataNormalizeAction.GetLegacyFilepaths(settingsService.GetSettings().GetDbPath())
                .ForEach(filepath => fileIOService.Delete(filepath));

            foreach (var (target, content) in plan.Files)
                await fileIOService.WriteBytes(target, content);

            savesLoadersService.Clear();
            await sessionService.StartNewSession(checkInitialActions: true, flags);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cloud sync: apply failed, rollback to the local safety backup");
            await backupService.RestoreBackup(safeBackupTime, withSafeBackup: false, flags);
            throw;
        }

        return new(plan.SavesUpdated, plan.SavesSkipped);
    }

    public record Plan(List<(string Target, byte[] Content)> Files, int SavesUpdated, int SavesSkipped);

    public static async Task<Plan> BuildPlan(byte[] zip, string mainDbPath, ISavesLoadersService savesLoadersService)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>();
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
                continue;
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            entries[ entry.FullName.Replace('\\', '/') ] = memory.ToArray();
        }

        if (!entries.TryGetValue("_paths.json", out var pathsBytes))
            throw new CloudSyncException(CloudSyncError.INVALID_BACKUP, "Backup without _paths.json");

        var paths = new Dictionary<string, string>();
        using (var doc = JsonDocument.Parse(pathsBytes))
        {
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                    paths[ property.Name.Replace('\\', '/') ] = property.Value.GetString()!;
            }
        }

        var localSavePaths = savesLoadersService.GetSaveById()
            .Where(pair => !string.IsNullOrEmpty(pair.Value.Metadata.FilePath))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Metadata.FilePath!);

        var dbFileName = Path.GetFileName(mainDbPath);
        var files = new List<(string Target, byte[] Content)>();
        var hasDb = false;
        var savesUpdated = 0;
        var savesSkipped = 0;

        foreach (var (name, content) in entries)
        {
            if (name == "_paths.json")
                continue;

            if (name.StartsWith("db/", StringComparison.Ordinal))
            {
                // legacy json db files are not carried between computers
                if (Path.GetFileName(name) == dbFileName)
                {
                    files.Add((mainDbPath, content));
                    hasDb = true;
                }
                continue;
            }

            if (name.StartsWith("saves/", StringComparison.Ordinal))
            {
                uint? saveId = null;
                try
                {
                    saveId = (await savesLoadersService.CheckSaveData(content, Path.GetFileName(name), overwrite: true)).Id;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"Cloud sync: cannot read save {name}, skipped");
                }

                if (saveId != null && localSavePaths.TryGetValue(saveId.Value, out var localPath))
                {
                    files.Add((localPath, content));
                    savesUpdated++;
                }
                else
                {
                    Log.Information($"Cloud sync: save {name} (id={saveId}) is not on this computer, skipped");
                    savesSkipped++;
                }
                continue;
            }

            if (name.StartsWith("main/", StringComparison.Ordinal))
            {
                if (!paths.TryGetValue(name, out var target))
                    throw new CloudSyncException(CloudSyncError.INVALID_BACKUP, $"No target path for {name}");

                if (Path.IsPathRooted(target))
                    throw new CloudSyncException(CloudSyncError.UNSUPPORTED_PATHS, $"Storage path is absolute on the other computer: {target}");

                files.Add((MatcherUtil.NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), target)), content));
            }
        }

        if (!hasDb)
            throw new CloudSyncException(CloudSyncError.INVALID_BACKUP, "Backup without main database");

        return new(files, savesUpdated, savesSkipped);
    }
}
