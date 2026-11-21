using System.IO.Abstractions.TestingHelpers;
using Moq;
using PKHeX.Core;
using PKVault.Core;
using PKVault.Core.SaveDiscovery;

public class SaveDiscoveryServiceTests
{
    private const string MgbaPortable = "D:/Emulation/Emladores/mGBA-0.10.5-win64";
    private const string RomFolder = "D:/Emulation/Roms/GBA/Sapphire";

    private static readonly StaticDiscoveryEnvironment WindowsEnv = new(
        DiscoveryOs.Windows,
        HomeFolder: "C:/Users/Ash",
        AppDataFolder: "C:/Users/Ash/AppData/Roaming",
        LocalAppDataFolder: "C:/Users/Ash/AppData/Local"
    );

    private readonly MockFileSystem fs = new();
    private readonly byte[] sapphireData;
    private readonly Mock<ISettingsService> settingsService = new();
    private readonly Mock<ISavesLoadersService> savesLoadersService = new();
    private string[] saveGlobs = [];

    public SaveDiscoveryServiceTests()
    {
        Program.Initialize();

        sapphireData = File.ReadAllBytes(Path.Combine(Program.InitialCurrentDirectory, "./assets/Pokemon - Version Saphir (France).srm"));

        settingsService.Setup(s => s.GetSettings()).Returns(() => CreateSettings(saveGlobs));
        // loaders not initialized by default
        savesLoadersService.Setup(s => s.GetAllSaveInfos()).Throws(new Exception("Save loaders not initialized"));
    }

    private static SettingsDTO CreateSettings(string[] globs) => new(
        BuildID: Guid.Empty,
        RuntimeSystem: RuntimeSystem.WINDOWS,
        SourceProvider: SourceProvider.GithubRelease,
        FlatpakMigrated: false,
        Version: "",
        PkhexVersion: "",
        AppDirectory: "",
        SettingsPath: "",
        UserId: "",
        SettingsMutable: new(
            DB_PATH: "./db",
            SAVE_GLOBS: globs,
            PKM_EXTERNAL_GLOBS: [],
            STORAGE_PATH: "./storage",
            BACKUP_PATH: "./backup",
            HIDE_CHEATS: false,
            SKIP_LEGALITY_CHECKS: false
        )
    );

    private SaveDiscoveryService CreateService(params IEmulatorDetector[] detectors)
    {
        var fileIOService = new FileIOService(fs);
        fileIOService.Matcher.GetAllPaths = (rootDir, globs) => [.. fs.AllFiles];

        return new(fs, WindowsEnv, detectors, fileIOService, settingsService.Object, savesLoadersService.Object);
    }

    private void AddPortableMgba()
    {
        fs.AddFile($"{MgbaPortable}/mGBA.exe", new MockFileData([0]));
        fs.AddFile($"{MgbaPortable}/portable.ini", new MockFileData(""));
        fs.AddFile($"{MgbaPortable}/config.ini", new MockFileData($"""
            [ports.qt]
            lastDirectory={RomFolder}
            savegamePath=
            """));
        fs.AddFile($"{RomFolder}/sapphire.gba", new MockFileData(new byte[64]));
        fs.AddFile($"{RomFolder}/sapphire.sav", new MockFileData(sapphireData));
        // not a save, but a candidate extension
        fs.AddFile($"{RomFolder}/garbage.sav", new MockFileData(new byte[0x20000]));
    }

    [Fact]
    public void Discover_PortableMgba_FindsSaveBesideRom()
    {
        AddPortableMgba();
        var service = CreateService(new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader()));

        var result = service.Discover([MgbaPortable], CancellationToken.None);

        var emulator = Assert.Single(result.Emulators);
        Assert.Equal("mgba", emulator.EmulatorId);
        Assert.True(emulator.Portable);

        var save = Assert.Single(result.Saves);
        Assert.Equal($"{RomFolder}/sapphire.sav", save.Path);
        Assert.Equal(3, save.Generation);
        Assert.Contains(save.Version, new[] { GameVersion.S, GameVersion.RS, GameVersion.RSE });
        Assert.False(string.IsNullOrWhiteSpace(save.TrainerName));
        Assert.Equal("mGBA", save.Source);
        Assert.Equal("mgba", save.EmulatorId);
        Assert.False(save.AlreadyAdded);
        Assert.Null(save.IsDuplicateOf);

        var folder = Assert.Single(result.Folders);
        Assert.Equal(RomFolder, folder.Path);
        Assert.Null(folder.Refusal);
        Assert.Equal(2, folder.CandidatesCount);
    }

    [Fact]
    public void Discover_ParentOfEmulatorInstalls_IsAlsoSearched()
    {
        AddPortableMgba();
        var service = CreateService(new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader()));

        var result = service.Discover(["D:/Emulation/Emladores"], CancellationToken.None);

        Assert.Single(result.Emulators);
        Assert.Single(result.Saves);
    }

    [Fact]
    public void Discover_DetectorFolderRefused_IsReportedNotScanned()
    {
        fs.AddFile($"{MgbaPortable}/portable.ini", new MockFileData(""));
        fs.AddFile($"{MgbaPortable}/config.ini", new MockFileData("[ports.qt]\nlastDirectory=C:/\nsavegamePath="));
        var service = CreateService(new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader()));

        var result = service.Discover([MgbaPortable], CancellationToken.None);

        var folder = Assert.Single(result.Folders);
        Assert.Equal(FolderScanRefusal.DRIVE_ROOT, folder.Refusal);
        Assert.Empty(result.Saves);
    }

    [Fact]
    public void ScanFolder_FindsSave_AndMarksAlreadyAdded()
    {
        AddPortableMgba();
        saveGlobs = [$"{RomFolder}/sapphire.sav"];
        var service = CreateService();

        var result = service.ScanFolder(RomFolder, CancellationToken.None);

        Assert.Null(result.Refusal);
        var save = Assert.Single(result.Saves);
        Assert.Equal(SaveDiscoveryService.FolderScanSource, save.Source);
        Assert.Null(save.EmulatorId);
        Assert.True(save.AlreadyAdded);
    }

    [Fact]
    public void ScanFolder_SameIdentityLoadedElsewhere_IsDuplicate()
    {
        AddPortableMgba();
        var loadedPath = "D:/Other/sapphire-copy.sav";

        SaveUtil.TryGetSaveFile((byte[])sapphireData.Clone(), out var loadedRaw);
        loadedRaw!.Metadata.SetExtraInfo(loadedPath);
        SaveLanguage.TryRevise(loadedRaw);
        var loaded = new SaveWrapper(loadedRaw);
        var loadedInfos = SaveInfosDTO.FromSave(loaded, null, DateTime.Now, []);

        savesLoadersService.Reset();
        savesLoadersService.Setup(s => s.GetAllSaveInfos()).Returns(new Dictionary<uint, SaveInfosDTO> { [loaded.Id] = loadedInfos });

        var result = CreateService().ScanFolder(RomFolder, CancellationToken.None);

        var save = Assert.Single(result.Saves);
        Assert.Equal(loaded.Id, save.SaveId);
        Assert.Equal(loadedPath, save.IsDuplicateOf);
        Assert.False(save.AlreadyAdded);
    }

    [Theory]
    [InlineData("C:/", FolderScanRefusal.DRIVE_ROOT)]
    [InlineData("C:/Windows", FolderScanRefusal.SYSTEM_FOLDER)]
    [InlineData("C:/Users/Ash", FolderScanRefusal.HOME_FOLDER)]
    [InlineData("relative", FolderScanRefusal.INVALID_PATH)]
    [InlineData("D:/Does/Not/Exist", FolderScanRefusal.NOT_FOUND)]
    public void ScanFolder_Refusals(string path, FolderScanRefusal expected)
    {
        fs.AddDirectory("C:/Windows");
        fs.AddDirectory("C:/Users/Ash");
        var result = CreateService().ScanFolder(path, CancellationToken.None);

        Assert.Equal(expected, result.Refusal);
        Assert.Empty(result.Saves);
        Assert.Equal(expected, Assert.Single(result.Folders).Refusal);
    }

    [Fact]
    public void ScanFolder_SupportsCancellation()
    {
        AddPortableMgba();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => CreateService().ScanFolder(RomFolder, cts.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => CreateService(
            new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader())
        ).Discover([MgbaPortable], cts.Token));
    }

    [Fact]
    public void PrepareAddPaths_AppendsValidSaves()
    {
        AddPortableMgba();
        saveGlobs = ["./pokemon-emerald-sample.sav"];

        var (globs, added) = CreateService().PrepareAddPaths([$"{RomFolder}\\sapphire.sav"]);

        Assert.Equal([$"{RomFolder}/sapphire.sav"], added);
        Assert.Equal(["./pokemon-emerald-sample.sav", $"{RomFolder}/sapphire.sav"], globs);
    }

    [Fact]
    public void PrepareAddPaths_SkipsAlreadyAdded()
    {
        AddPortableMgba();
        saveGlobs = [$"{RomFolder}/sapphire.sav"];

        var (globs, added) = CreateService().PrepareAddPaths([$"{RomFolder}/sapphire.sav"]);

        Assert.Empty(added);
        Assert.Equal(saveGlobs, globs);
    }

    [Theory]
    [InlineData(RomFolder + "/garbage.sav")]
    [InlineData(RomFolder + "/missing.sav")]
    [InlineData(RomFolder + "/*.sav")]
    [InlineData("relative.sav")]
    public void PrepareAddPaths_RejectsInvalid(string path)
    {
        AddPortableMgba();
        Assert.Throws<ArgumentException>(() => CreateService().PrepareAddPaths([path]));
    }

    [Fact]
    public void Discovery_NeverWritesFiles()
    {
        AddPortableMgba();
        var before = fs.AllPaths.ToDictionary(p => p, p => fs.FileExists(p) ? fs.GetFile(p).Contents.Length : -1);

        var service = CreateService(new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader()));
        service.Discover([MgbaPortable], CancellationToken.None);
        service.ScanFolder(RomFolder, CancellationToken.None);
        service.PrepareAddPaths([$"{RomFolder}/sapphire.sav"]);

        var after = fs.AllPaths.ToDictionary(p => p, p => fs.FileExists(p) ? fs.GetFile(p).Contents.Length : -1);
        Assert.Equal(before, after);
    }
}
