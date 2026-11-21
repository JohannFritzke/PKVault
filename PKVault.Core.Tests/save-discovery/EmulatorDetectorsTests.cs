using System.IO.Abstractions.TestingHelpers;
using PKVault.Core.SaveDiscovery;

public class FakeMgbaLibraryReader(params string[] paths) : IMgbaLibraryReader
{
    public List<string> ReadCalls { get; } = [];

    public IReadOnlyList<string> ReadRomPaths(string libraryPath)
    {
        ReadCalls.Add(libraryPath);
        return paths;
    }
}

public class EmulatorDetectorsTests
{
    private const string MgbaPortable = "D:/Emulation/Emladores/mGBA-0.10.5-win64";
    private const string FireRedFolder = "D:/Emulation/Roms/GBA/Pokemon - Fire Red Version [a1] (U) (Squirrels)";

    private readonly MockFileSystem fs = new();

    private static readonly StaticDiscoveryEnvironment WindowsEnv = new(
        DiscoveryOs.Windows,
        HomeFolder: "C:/Users/Ash",
        AppDataFolder: "C:/Users/Ash/AppData/Roaming",
        LocalAppDataFolder: "C:/Users/Ash/AppData/Local"
    );

    private static readonly StaticDiscoveryEnvironment LinuxEnv = new(
        DiscoveryOs.Linux,
        HomeFolder: "/home/ash",
        XdgConfigHome: "/home/ash/.config",
        XdgDataHome: "/home/ash/.local/share"
    );

    // modeled on the real portable mGBA 0.10.5 config.ini (trimmed)
    private static string MgbaConfig(string savegamePath, string lastDirectory) => $"""
        [gba.input.QT_K]
        keyA=88

        [ports.qt]
        fullscreen=0
        savestatePath=
        lastDirectory={lastDirectory}
        cheatsPath=
        screenshotPath=
        savegamePath={savegamePath}
        patchPath=
        """;

    private const string MgbaQtIni = $"""
        [General]
        maximized=false

        [mru]
        0={FireRedFolder}/1636 - Pokemon Fire Red (U)(Squirrels).gba
        1=E:/Other Roms/emerald.gba
        """;

    private void AddPortableMgba(string savegamePath = "", string lastDirectory = FireRedFolder)
    {
        fs.AddFile($"{MgbaPortable}/mGBA.exe", new MockFileData([0]));
        fs.AddFile($"{MgbaPortable}/portable.ini", new MockFileData(""));
        fs.AddFile($"{MgbaPortable}/config.ini", new MockFileData(MgbaConfig(savegamePath, lastDirectory)));
        fs.AddFile($"{MgbaPortable}/qt.ini", new MockFileData(MgbaQtIni));
        fs.AddFile($"{FireRedFolder}/1636 - Pokemon Fire Red (U)(Squirrels).gba", new MockFileData([0]));
        fs.AddFile($"{FireRedFolder}/1636 - Pokemon Fire Red (U)(Squirrels).sav", new MockFileData([0]));
    }

    [Fact]
    public void Mgba_Portable_EmptySavegamePath_UsesRomFolders()
    {
        AddPortableMgba();
        var detector = new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader());

        var detected = detector.DetectAt(MgbaPortable);

        Assert.NotNull(detected);
        Assert.Equal("mgba", detected.EmulatorId);
        Assert.True(detected.Portable);
        Assert.Equal($"{MgbaPortable}/config.ini", detected.ConfigPath);
        Assert.Equal([
            new(FireRedFolder, EmulatorSaveFolderOrigin.ROM_FOLDER),  // lastDirectory + mru (deduplicated)
            new("E:/Other Roms", EmulatorSaveFolderOrigin.ROM_FOLDER),
        ], detected.SaveFolders);
    }

    [Fact]
    public void Mgba_NonEmptySavegamePath_UsesThatFolderOnly()
    {
        AddPortableMgba(savegamePath: "D:/Emulation/Saves/GBA");
        var detector = new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader());

        var detected = detector.DetectAt(MgbaPortable);

        Assert.NotNull(detected);
        Assert.Equal([new EmulatorSaveFolder("D:/Emulation/Saves/GBA", EmulatorSaveFolderOrigin.CONFIG_SAVE_PATH)], detected.SaveFolders);
    }

    [Fact]
    public void Mgba_RelativeSavegamePath_IsResolvedAgainstConfigFolder()
    {
        AddPortableMgba(savegamePath: "saves");
        var detector = new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader());

        var detected = detector.DetectAt(MgbaPortable);

        Assert.Equal($"{MgbaPortable}/saves", detected!.SaveFolders.Single().Path);
    }

    [Fact]
    public void Mgba_Library_RootsAndRomPaths_AreRomFolders()
    {
        AddPortableMgba(lastDirectory: "");
        fs.AddFile($"{MgbaPortable}/library.sqlite3", new MockFileData([0]));
        fs.AddFile("F:/Library/Gen3/sapphire.zip", new MockFileData([0]));
        fs.AddDirectory("F:/Library/Gen1");

        var reader = new FakeMgbaLibraryReader(
            "F:/Library/Gen1",                       // root folder
            "F:/Library/Gen3/sapphire.zip",          // root archive -> its folder
            "F:/Library/Gen3/sapphire.zip/sapphire.gba" // rom inside archive -> archive folder
        );
        var detector = new MgbaDetector(fs, WindowsEnv, reader);

        var detected = detector.DetectAt(MgbaPortable);

        Assert.Equal([$"{MgbaPortable}/library.sqlite3"], reader.ReadCalls);
        var paths = detected!.SaveFolders.Select(f => f.Path).ToArray();
        Assert.Contains("F:/Library/Gen1", paths);
        Assert.Contains("F:/Library/Gen3", paths);
        Assert.DoesNotContain("F:/Library/Gen3/sapphire.zip", paths);
    }

    [Fact]
    public void Mgba_AppDataInstall_IsNotPortable_AndDefaultLocationsPerOs()
    {
        var appDataFolder = "C:/Users/Ash/AppData/Roaming/mGBA";
        fs.AddFile($"{appDataFolder}/config.ini", new MockFileData(MgbaConfig("", FireRedFolder)));

        var detector = new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader());
        Assert.Equal([appDataFolder], detector.GetDefaultLocations());

        var detected = detector.DetectAt(appDataFolder);
        Assert.NotNull(detected);
        Assert.False(detected.Portable);
        Assert.Equal(FireRedFolder, detected.SaveFolders.Single().Path);

        var linuxDetector = new MgbaDetector(fs, LinuxEnv, new FakeMgbaLibraryReader());
        Assert.Contains("/home/ash/.config/mgba", linuxDetector.GetDefaultLocations());
        Assert.Contains("/home/ash/.var/app/io.mgba.mGBA/config/mgba", linuxDetector.GetDefaultLocations());
    }

    [Fact]
    public void Mgba_FolderWithoutConfig_IsNotDetected()
    {
        fs.AddFile("D:/Random/config.ini", new MockFileData("[foo]\nbar=1"));
        fs.AddDirectory("D:/Empty");
        var detector = new MgbaDetector(fs, WindowsEnv, new FakeMgbaLibraryReader());

        Assert.Null(detector.DetectAt("D:/Empty"));
        Assert.Null(detector.DetectAt("D:/Random"));
    }

    [Fact]
    public void MelonDs_Ini_EmptySaveFilePath_UsesRomFolders()
    {
        fs.AddFile("D:/Emu/melonDS/melonDS.exe", new MockFileData([0]));
        fs.AddFile("D:/Emu/melonDS/melonDS.ini", new MockFileData("""
            SaveFilePath=
            LastROMFolder=D:/Roms/NDS
            RecentROM_0=D:/Roms/NDS/Platinum/platinum.nds
            """));

        var detected = new MelonDsDetector(fs, WindowsEnv).DetectAt("D:/Emu/melonDS");

        Assert.NotNull(detected);
        Assert.True(detected.Portable);
        Assert.Equal(["D:/Roms/NDS", "D:/Roms/NDS/Platinum"], detected.SaveFolders.Select(f => f.Path));
    }

    [Fact]
    public void MelonDs_Toml_SaveFilePath_IsUsed()
    {
        fs.AddFile("/home/ash/.config/melonDS/melonDS.toml", new MockFileData("""
            [Instance0]
            SaveFilePath = "/home/ash/nds-saves"
            RecentROM = [ "/home/ash/roms/hgss.nds", ]
            """));

        var detected = new MelonDsDetector(fs, LinuxEnv).DetectAt("/home/ash/.config/melonDS");

        Assert.Equal([new EmulatorSaveFolder("/home/ash/nds-saves", EmulatorSaveFolderOrigin.CONFIG_SAVE_PATH)], detected!.SaveFolders);
    }

    [Fact]
    public void Desmume_Ini_BatteryPath()
    {
        fs.AddFile("D:/Emu/DeSmuME/desmume.ini", new MockFileData("""
            [PathSettings]
            Battery=.\Battery
            """));

        var detected = new DesmumeDetector(fs, WindowsEnv).DetectAt("D:/Emu/DeSmuME");

        Assert.Equal("D:/Emu/DeSmuME/Battery", detected!.SaveFolders.Single().Path);
    }

    [Fact]
    public void Citra_FindsTitleFolders()
    {
        var sdmc = "C:/Users/Ash/AppData/Roaming/Citra/sdmc/Nintendo 3DS";
        fs.AddFile($"{sdmc}/0000/1111/title/00040000/00055d00/data/00000001/main", new MockFileData([0]));

        var detector = new CitraDetector(fs, WindowsEnv);
        Assert.Contains("C:/Users/Ash/AppData/Roaming/Citra", detector.GetDefaultLocations());

        var detected = detector.DetectAt("C:/Users/Ash/AppData/Roaming/Citra");
        Assert.False(detected!.Portable);
        Assert.Equal($"{sdmc}/0000/1111/title/00040000", detected.SaveFolders.Single().Path);

        // portable: user/ beside the exe
        fs.AddFile("D:/Emu/citra/user/sdmc/Nintendo 3DS/a/b/title/00040000/x/data/00000001/main", new MockFileData([0]));
        var portable = detector.DetectAt("D:/Emu/citra");
        Assert.True(portable!.Portable);
    }

    [Fact]
    public void RetroArch_SaveDirectory_AndContentDir()
    {
        fs.AddFile("D:/RetroArch/retroarch.exe", new MockFileData([0]));
        fs.AddFile("D:/RetroArch/retroarch.cfg", new MockFileData("""
            savefile_directory = ":\saves"
            savefiles_in_content_dir = "true"
            rgui_browser_directory = "default"
            """));
        fs.AddFile("D:/RetroArch/playlists/builtin/content_history.lpl", new MockFileData("""
            { "version": "1.5", "items": [ { "path": "E:\\Roms\\GBA\\ruby.zip#ruby.gba" } ] }
            """));
        fs.AddFile("E:/Roms/GBA/ruby.zip", new MockFileData([0]));

        var detected = new RetroArchDetector(fs, WindowsEnv).DetectAt("D:/RetroArch");

        Assert.NotNull(detected);
        Assert.True(detected.Portable);
        Assert.Equal([
            new("D:/RetroArch/saves", EmulatorSaveFolderOrigin.CONFIG_SAVE_PATH),
            new("E:/Roms/GBA", EmulatorSaveFolderOrigin.ROM_FOLDER),
        ], detected.SaveFolders);
    }

    [Fact]
    public void RetroArch_DefaultSaveDirectory()
    {
        fs.AddFile("/home/ash/.config/retroarch/retroarch.cfg", new MockFileData("savefile_directory = \"default\""));

        var detected = new RetroArchDetector(fs, LinuxEnv).DetectAt("/home/ash/.config/retroarch");

        Assert.Equal([new EmulatorSaveFolder("/home/ash/.config/retroarch/saves", EmulatorSaveFolderOrigin.DEFAULT_SAVE_PATH)], detected!.SaveFolders);
    }

    [Fact]
    public void LooseConfig_ParsesIniTomlAndCfg()
    {
        var config = LooseConfig.Parse("""
            ; comment
            [ports.qt]
            savegamePath=
            lastDirectory=D:/Roms/A [b] (c)
            [Instance0]
            Path = "C:\\Games\\x"
            List = [
              "a",
              "b"
            ]
            """);

        Assert.Equal("", config.Get("savegamePath", "ports.qt"));
        Assert.Equal("D:/Roms/A [b] (c)", config.Get("lastdirectory"));
        Assert.Equal(@"C:\Games\x", config.Get("Path", "Instance0"));
        Assert.Equal(["a", "b"], config.Find("List").Single().Values);
        Assert.True(config.HasSectionStartingWith("ports."));
    }
}
