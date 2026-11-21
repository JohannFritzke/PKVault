using System.IO.Abstractions.TestingHelpers;
using PKVault.Core.SaveDiscovery;

public class SaveFileScannerTests
{
    private readonly MockFileSystem fs = new();
    private readonly SaveFileScanner scanner;

    public SaveFileScannerTests()
    {
        scanner = new(fs);
    }

    private static MockFileData Data(int size) => new(new byte[size]);

    private string[] ScanPaths(string root, SaveScanOptions? options = null) => [..
        scanner.Scan(root, options ?? new(), CancellationToken.None).Candidates.Select(c => c.Path).Order()
    ];

    [Fact]
    public void Scan_KeepsOnlyCandidateExtensionsAndNames()
    {
        fs.AddFile("D:/Saves/emerald.sav", Data(0x20000));
        fs.AddFile("D:/Saves/platinum.dsv", Data(0x80000));
        fs.AddFile("D:/Saves/ruby.SRM", Data(0x20000));
        fs.AddFile("D:/Saves/firered.sa1", Data(0x20000));
        fs.AddFile("D:/Saves/3ds/main", Data(0x80000));
        fs.AddFile("D:/Saves/firered.gba", Data(0x1000));
        fs.AddFile("D:/Saves/firered.ss1", Data(0x1000));
        fs.AddFile("D:/Saves/firered.sav.old", Data(0x20000));
        fs.AddFile("D:/Saves/readme.txt", Data(10));

        Assert.Equal([
            "D:/Saves/3ds/main",
            "D:/Saves/emerald.sav",
            "D:/Saves/firered.sa1",
            "D:/Saves/platinum.dsv",
            "D:/Saves/ruby.SRM",
        ], ScanPaths("D:/Saves"));
    }

    [Fact]
    public void Scan_SkipsEmptyAndTooBigFiles()
    {
        fs.AddFile("D:/Saves/empty.sav", Data(0));
        fs.AddFile("D:/Saves/huge.bin", Data(3 * 1024 * 1024 + 1));
        fs.AddFile("D:/Saves/limit.bin", Data(3 * 1024 * 1024));

        Assert.Equal(["D:/Saves/limit.bin"], ScanPaths("D:/Saves"));
    }

    [Fact]
    public void Scan_RespectsMaxDepth()
    {
        fs.AddFile("D:/R/d0.sav", Data(16));
        fs.AddFile("D:/R/1/d1.sav", Data(16));
        fs.AddFile("D:/R/1/2/d2.sav", Data(16));
        fs.AddFile("D:/R/1/2/3/d3.sav", Data(16));
        fs.AddFile("D:/R/1/2/3/4/d4.sav", Data(16));
        fs.AddFile("D:/R/1/2/3/4/5/d5.sav", Data(16));

        var paths = ScanPaths("D:/R");
        Assert.Contains("D:/R/1/2/3/4/d4.sav", paths);
        Assert.DoesNotContain("D:/R/1/2/3/4/5/d5.sav", paths);
        Assert.Equal(5, paths.Length);

        Assert.Equal(["D:/R/1/d1.sav", "D:/R/d0.sav"], ScanPaths("D:/R", new(MaxDepth: 1)));
    }

    [Fact]
    public void Scan_StopsAtFileCap_AndFlagsTruncated()
    {
        for (var i = 0; i < 30; i++)
            fs.AddFile($"D:/Many/file{i:D2}.txt", Data(4));
        fs.AddFile("D:/Many/sub/late.sav", Data(16));

        var result = scanner.Scan("D:/Many", new(MaxFilesVisited: 10), CancellationToken.None);

        Assert.True(result.Truncated);
        Assert.Equal(10, result.FilesVisited);
        Assert.Empty(result.Candidates);

        var full = scanner.Scan("D:/Many", new(MaxFilesVisited: 100), CancellationToken.None);
        Assert.False(full.Truncated);
        Assert.Single(full.Candidates);
    }

    [Fact]
    public void Scan_SkipsHeavyHiddenAndDotFolders()
    {
        fs.AddFile("D:/P/node_modules/x/a.sav", Data(16));
        fs.AddFile("D:/P/.git/objects/b.bin", Data(16));
        fs.AddFile("D:/P/.hidden/c.sav", Data(16));
        fs.AddFile("D:/P/winhidden/d.sav", Data(16));
        fs.File.SetAttributes("D:/P/winhidden", FileAttributes.Directory | FileAttributes.Hidden);
        fs.AddFile("D:/P/ok/e.sav", Data(16));

        Assert.Equal(["D:/P/ok/e.sav"], ScanPaths("D:/P"));
    }

    [Fact]
    public void Scan_DoesNotFollowSymlinksOrJunctions()
    {
        fs.AddFile("D:/Loop/real/a.sav", Data(16));
        fs.AddFile("D:/Elsewhere/secret.sav", Data(16));

        // symlink pointing outside
        fs.Directory.CreateSymbolicLink("D:/Loop/link", "D:/Elsewhere");
        // junction-like folder (reparse point attribute)
        fs.AddFile("D:/Loop/junction/b.sav", Data(16));
        fs.File.SetAttributes("D:/Loop/junction", FileAttributes.Directory | FileAttributes.ReparsePoint);

        Assert.Equal(["D:/Loop/real/a.sav"], ScanPaths("D:/Loop"));
    }

    [Fact]
    public void Scan_SupportsCancellation()
    {
        fs.AddFile("D:/C/a.sav", Data(16));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => scanner.Scan("D:/C", new(), cts.Token));
    }

    [Fact]
    public void Scan_MissingFolder_ReturnsEmpty()
    {
        var result = scanner.Scan("D:/Nope", new(), CancellationToken.None);
        Assert.Empty(result.Candidates);
        Assert.False(result.Truncated);
    }
}
