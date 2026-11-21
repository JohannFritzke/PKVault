using System.IO.Abstractions;
using PKHeX.Core;
using Serilog;

namespace PKVault.Core.SaveDiscovery;

public record ValidatedSave(string Path, SaveWrapper Save, long FileSize, DateTime LastWriteTime);

/// <summary>
/// Opens a candidate file read-only and asks PKHeX if it is a save.
/// Mirrors SavesLoadersService save loading so SaveWrapper.Id matches loaded saves.
/// </summary>
public class SaveCandidateValidator(IFileSystem fileSystem)
{
    public ValidatedSave? TryValidate(string path, long maxFileSize = long.MaxValue)
    {
        try
        {
            var fileInfo = fileSystem.FileInfo.New(path);
            if (!fileInfo.Exists)
                return null;

            var length = fileInfo.Length;
            if (length > maxFileSize || FileUtil.IsFileTooSmall(length) || FileUtil.IsFileTooBig(length))
                return null;

            byte[] data;
            // read-only, and tolerant to emulators holding the file open
            using (var stream = fileSystem.FileStream.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                data = new byte[stream.Length];
                stream.ReadExactly(data);
            }

            if (!SaveUtil.TryGetSaveFile(data, out var saveRaw))
                return null;

            saveRaw.Metadata.SetExtraInfo(path);
            if (saveRaw.Generation <= 3)
                SaveLanguage.TryRevise(saveRaw);

            return new(path, new SaveWrapper(saveRaw), length, fileInfo.LastWriteTime);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Save discovery: cannot read candidate {path}");
            return null;
        }
    }
}
