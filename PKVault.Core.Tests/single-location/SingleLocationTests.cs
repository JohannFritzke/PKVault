using System.Collections.Immutable;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PKHeX.Core;
using PKVault.Core;

/// <summary>
/// Fork rule: a pokémon is either in a save or in the storage.
/// </summary>
public class SingleLocationTests : IAsyncDisposable
{
    private SessionDbContext? db;

    public async ValueTask DisposeAsync()
    {
        if (db != null)
            await db.Database.EnsureDeletedAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AttachedMovesAndDetach_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => SingleLocation.ThrowIfAttached(true));
        SingleLocation.ThrowIfAttached(false);
    }

    [Theory]
    [InlineData(EntityContext.Gen3, 3, false)]
    [InlineData(EntityContext.Gen3, 4, false)]
    [InlineData(EntityContext.Gen3, 2, true)]
    [InlineData(EntityContext.Gen8b, 8, false)]
    [InlineData(EntityContext.Gen8b, 7, true)]
    public void OlderGeneration_IsRefused(EntityContext mainContext, byte saveGeneration, bool refused)
    {
        List<PkmVariantEntity> variants = [
            Entity(mainContext, isMain: true),
            Entity(EntityContext.Gen1, isMain: false),
        ];

        var ex = Record.Exception(() => SingleLocation.ThrowIfOlderGeneration(variants, saveGeneration));

        Assert.Equal(refused, ex is ArgumentException);
    }

    [Fact]
    public async Task Resolve_KeepsInSave_UnlinksMissing_LeavesOthers()
    {
        var variants = await NewVariants();
        var box = (await BoxLoader.GetDto("1"))!;

        // slot 0: attached to save 10, which has it -> removed from the storage
        var attached = await variants.AddEntity(new(box, 0, true, false, 10, "id-attached", EntityContext.Gen3, 3, Pkm(1000)));
        // slot 1: a copy also in save 10 (after "detach") -> removed from the storage, with its other variant
        var copy = await variants.AddEntity(new(box, 1, true, false, null, null, EntityContext.Gen3, 3, Pkm(1001)));
        await variants.AddEntity(new(box, 1, false, false, null, null, EntityContext.Gen4, 4, Pkm4(1001)));
        // slot 2: attached to save 99, not loaded -> stays in the storage, link removed
        var missing = await variants.AddEntity(new(box, 2, true, false, 99, "id-missing", EntityContext.Gen3, 3, Pkm(1002)));
        // slot 3: only in the storage -> untouched
        var alone = await variants.AddEntity(new(box, 3, true, false, null, null, EntityContext.Gen3, 3, Pkm(1003)));

        var saves = SavesWith(10, [ "id-attached", copy.Hash ]);

        var conflicts = await ResolveSingleLocationAction.FindConflicts(variants, saves);
        Assert.Equal(3, conflicts.Count);
        Assert.Equal(2, conflicts.Count(c => c.InSave));

        var action = new ResolveSingleLocationAction(variants, saves);
        await action.ExecuteWithPayload(new(), new());

        Assert.Null(await variants.GetEntity(attached.Id));
        Assert.Empty(await variants.GetEntitiesByBox("1", 1));

        var unlinked = await variants.GetEntity(missing.Id);
        Assert.NotNull(unlinked);
        Assert.Null(unlinked.AttachedSaveId);
        Assert.Null(unlinked.AttachedSavePkmIdBase);

        Assert.NotNull(await variants.GetEntity(alone.Id));
        Assert.Empty(await ResolveSingleLocationAction.FindConflicts(variants, saves));
    }

    private BoxLoader BoxLoader = default!;

    private async Task<PkmVariantLoader> NewVariants()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>(), Directory.GetCurrentDirectory());
        var fileIO = new FileIOService(fs);
        fileIO.Matcher.GetAllPaths = (rootDir, globs) => [ .. fs.AllFiles ];

        var session = new Mock<ISessionServiceMinimal>();
        session.Setup(s => s.SessionDbPath).Returns($"db-SingleLocationTests-{Guid.NewGuid()}.db");
        db = new(session.Object, new Mock<DbSeedingService>(fileIO).Object);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetSettings()).Returns(new SettingsDTO(
            BuildID: default, RuntimeSystem: RuntimeSystem.LINUX, SourceProvider: SourceProvider.GithubRelease, FlatpakMigrated: false,
            Version: "", PkhexVersion: "", AppDirectory: "app", SettingsPath: "", UserId: "",
            SettingsMutable: new(
                DB_PATH: "mock-db", SAVE_GLOBS: [], PKM_EXTERNAL_GLOBS: [], STORAGE_PATH: "mock-storage", BACKUP_PATH: "mock-bkp",
                LANGUAGE: "en", HIDE_CHEATS: false, SKIP_LEGALITY_CHECKS: false
            )
        ));
        var staticData = new StaticDataService(settings.Object);
        var pkmFiles = new PkmFileLoader(fileIO, session.Object, settings.Object, db);
        var variants = new PkmVariantLoader(fileIO, session.Object, settings.Object, pkmFiles, db, staticData);

        var services = new ServiceCollection();
        services.AddScoped(_ => new Mock<IInventoryLoader>().Object);
        BoxLoader = new BoxLoader(session.Object, db, variants, services.BuildServiceProvider());

        await db.Banks.AddAsync(new() { Id = "1", IdInt = 1, IsDefault = true, IsExternal = false, Name = "Bank 1", Order = 0, View = new([], []) }, TestContext.Current.CancellationToken);
        await db.Boxes.AddAsync(new() { Id = "1", IdInt = 1, Name = "Box 1", Order = 0, Type = BoxType.Box, SlotCount = 30, BankId = "1" }, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return variants;
    }

    /// <summary>One loaded save holding the given pokémon ids (IdBase).</summary>
    private static ISavesLoadersService SavesWith(uint saveId, string[] idBases)
    {
        var pkms = new Mock<ISavePkmLoader>();
        pkms.Setup(p => p.GetDtosByIdBase(It.IsAny<string>()))
            .Returns((string idBase) => idBases.Contains(idBase)
                ? ImmutableDictionary<string, PkmSaveDTO>.Empty.Add(idBase, null!)
                : ImmutableDictionary<string, PkmSaveDTO>.Empty);

        var record = new SaveLoadersRecord(null!, new Mock<ISaveBoxLoader>().Object, pkms.Object, new Mock<ISaveInventoryLoader>().Object, []);

        var saves = new Mock<ISavesLoadersService>();
        saves.Setup(s => s.GetAllLoaders()).Returns([ record ]);
        saves.Setup(s => s.GetLoaders(saveId)).Returns(record);
        return saves.Object;
    }

    private static PkmVariantEntity Entity(EntityContext context, bool isMain) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Hash = "",
        BoxId = "1",
        BoxSlot = 0,
        IsMain = isMain,
        IsExternal = false,
        AttachedSaveId = null,
        AttachedSavePkmIdBase = null,
        Context = context,
        Generation = 3,
        Filepath = "",
        Species = 133,
        Form = 0,
        Gender = Gender.Male,
        IsShiny = false,
        IsAlpha = false,
        PkmFile = null!,
    };

    private static ImmutablePKM Pkm(uint pid)
    {
        var pk = new PK3 { Species = 133, PID = pid, TID16 = 54321 };
        pk.RefreshChecksum();
        return new ImmutablePKM(pk);
    }

    private static ImmutablePKM Pkm4(uint pid)
    {
        var pk = new PK4 { Species = 133, PID = pid, TID16 = 54321 };
        pk.RefreshChecksum();
        return new ImmutablePKM(pk);
    }
}
