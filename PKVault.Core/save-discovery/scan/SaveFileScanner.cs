using System.IO.Abstractions;

namespace PKVault.Core.SaveDiscovery;

public record SaveScanOptions(
    // number of nested sub-folders below the scanned folder
    int MaxDepth = 4,
    // files visited (any extension) before the scan stops and is flagged truncated
    int MaxFilesVisited = 5000,
    long MaxFileSize = 3 * 1024 * 1024
);

public record SaveScanCandidate(string Path, long Size);

public record SaveScanResult(
    string Root,
    IReadOnlyList<SaveScanCandidate> Candidates,
    int FilesVisited,
    bool Truncated
);

/// <summary>
/// Read-only, bounded folder walk that lists files which may be saves (by extension/name and size).
/// Never follows symlinks/junctions, skips hidden/system folders and well-known heavy folders.
/// </summary>
public class SaveFileScanner(IFileSystem fileSystem)
{
    public static readonly HashSet<string> CandidateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // generic battery saves (mGBA, melonDS, NO$GBA, VBA, mostly all emulators & dumpers)
        ".sav", ".srm", ".sa1", ".sa2", ".fla",
        // DeSmuME
        ".dsv",
        // raw dumps, GameCube memory card / Dolphin, Action Replay / GameShark
        ".dat", ".bin", ".raw", ".dmp", ".ram", ".gci", ".gcs", ".duc", ".dss",
    };

    // extension-less save names (3DS / Switch extracted saves, Citra, Ryujinx...)
    public static readonly HashSet<string> CandidateFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "main",
    };

    public static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".svn", ".hg", "__pycache__", "$Recycle.Bin", "System Volume Information",
        "shaders", "cheats", "screenshots", "states", "savestates", "thumbnails", "cores", "assets",
    };

    public static bool IsCandidateName(string fileName)
    {
        if (CandidateFileNames.Contains(fileName))
            return true;

        var extension = Path.GetExtension(fileName);
        return extension.Length > 0 && CandidateExtensions.Contains(extension);
    }

    public SaveScanResult Scan(string root, SaveScanOptions options, CancellationToken cancellationToken)
    {
        var normalizedRoot = DiscoveryPath.Normalize(root) ?? root;

        List<SaveScanCandidate> candidates = [];
        HashSet<string> visitedDirectories = new(StringComparer.OrdinalIgnoreCase);
        Queue<(string Path, int Depth)> queue = new();
        queue.Enqueue((normalizedRoot, 0));

        var filesVisited = 0;
        var truncated = false;

        while (queue.Count > 0 && !truncated)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (directory, depth) = queue.Dequeue();
            if (!visitedDirectories.Add(directory))
                continue;

            IEnumerable<IFileSystemInfo> entries;
            try
            {
                entries = fileSystem.DirectoryInfo.New(directory).EnumerateFileSystemInfos();
            }
            catch (Exception ex) when (IsAccessException(ex))
            {
                continue;
            }

            try
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry is IDirectoryInfo subDirectory)
                    {
                        if (depth < options.MaxDepth && ShouldEnterDirectory(subDirectory))
                        {
                            var subPath = DiscoveryPath.Normalize(subDirectory.FullName);
                            if (subPath != null)
                                queue.Enqueue((subPath, depth + 1));
                        }
                        continue;
                    }

                    if (entry is not IFileInfo file)
                        continue;

                    filesVisited++;
                    if (filesVisited > options.MaxFilesVisited)
                    {
                        filesVisited = options.MaxFilesVisited;
                        truncated = true;
                        break;
                    }

                    if (!IsCandidateName(file.Name) || IsLink(file))
                        continue;

                    long length;
                    try
                    {
                        length = file.Length;
                    }
                    catch (Exception ex) when (IsAccessException(ex))
                    {
                        continue;
                    }

                    if (length <= 0 || length > options.MaxFileSize)
                        continue;

                    var filePath = DiscoveryPath.Normalize(file.FullName);
                    if (filePath != null)
                        candidates.Add(new(filePath, length));
                }
            }
            catch (Exception ex) when (IsAccessException(ex))
            {
                // folder became inaccessible during enumeration, keep what was found
            }
        }

        return new(normalizedRoot, candidates, filesVisited, truncated);
    }

    private static bool ShouldEnterDirectory(IDirectoryInfo directory)
    {
        if (SkippedDirectoryNames.Contains(directory.Name) || directory.Name.StartsWith('.'))
            return false;

        if (IsLink(directory))
            return false;

        try
        {
            var attributes = directory.Attributes;
            if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System))
                return false;
        }
        catch (Exception ex) when (IsAccessException(ex))
        {
            return false;
        }

        return true;
    }

    // symlinks & junctions (avoid loops & escaping the scanned folder)
    private static bool IsLink(IFileSystemInfo info)
    {
        try
        {
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget != null;
        }
        catch (Exception ex) when (IsAccessException(ex))
        {
            return true;
        }
    }

    private static bool IsAccessException(Exception ex) => ex is UnauthorizedAccessException
        or IOException
        or System.Security.SecurityException
        or ArgumentException
        or NotSupportedException;
}
