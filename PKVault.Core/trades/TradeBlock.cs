using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PKVault.Core.Trades;

/// <summary>Why a storage pokémon cannot be traded (shown in the boxes and the trade picker).</summary>
public enum TradeBlockReason
{
    /// <summary>Another copy exists in a save or elsewhere in the storage: trading it would duplicate it.</summary>
    DUPLICATE,
    /// <summary>PKHeX finds it impossible (always checked for trades, even with "skip legality checks").</summary>
    ILLEGAL,
}

public record TradeBlockDTO(string PkmVariantId, TradeBlockReason Reason, string? Detail);

public static class TradeLegality
{
    /// <summary>PKHeX legality without a save, ignoring the "skip legality checks" setting.</summary>
    public static bool IsLegal(ImmutablePKM pkm)
    {
        try
        {
            var la = new LegalityAnalysisWrapper(LegalityAnalysisService.GetLegalitySafeRaw(pkm));
            return la.Parsed && la.Valid;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Trades: legality check failed");
            return false;
        }
    }
}

/// <summary>
/// Where each pokémon (by IdBase hash) is: storage slots and loaded saves. Built once per request.
/// </summary>
public sealed class TradeCopyIndex
{
    private readonly ILookup<string, PkmVariantEntity> storageByHash;
    private readonly Dictionary<string, string> boxNames;
    private readonly List<SaveLoadersRecord> saves;

    private TradeCopyIndex(ILookup<string, PkmVariantEntity> storageByHash, Dictionary<string, string> boxNames, List<SaveLoadersRecord> saves)
    {
        this.storageByHash = storageByHash;
        this.boxNames = boxNames;
        this.saves = saves;
    }

    public static async Task<TradeCopyIndex> Build(IServiceProvider scoped, IServiceProvider root)
    {
        var loader = scoped.GetRequiredService<IPkmVariantLoader>();
        var all = (await loader.GetAllEntities()).Values;

        var boxLoader = scoped.GetService<IBoxLoader>();
        var boxNames = boxLoader == null
            ? []
            : (await boxLoader.GetAllEntities()).Values.ToDictionary(b => b.Id, b => b.Name);

        var saves = root.GetService<ISavesLoadersService>()?.GetAllLoaders().ToList() ?? [];

        return new(all.Where(v => !string.IsNullOrEmpty(v.Hash)).ToLookup(v => v.Hash), boxNames, saves);
    }

    /// <summary>Where another copy of the pokémon of this slot is, or null when there is none.</summary>
    public string? FindOtherCopy(IReadOnlyCollection<PkmVariantEntity> slotVariants, string boxId, int boxSlot)
    {
        var hashes = slotVariants.Select(v => v.Hash).Where(h => !string.IsNullOrEmpty(h)).ToHashSet();

        var elsewhere = hashes.SelectMany(hash => storageByHash[ hash ])
            .FirstOrDefault(v => v.BoxId != boxId || v.BoxSlot != boxSlot);
        if (elsewhere != null)
        {
            Log.Debug($"Trades: {boxId}/{boxSlot} has a copy {elsewhere.Id} at {elsewhere.BoxId}/{elsewhere.BoxSlot}"
                + $" main={elsewhere.IsMain} external={elsewhere.IsExternal} attached={elsewhere.AttachedSaveId} file={elsewhere.Filepath}");
            var where = elsewhere.IsExternal
                ? "external files"
                : $"storage box \"{(boxNames.TryGetValue(elsewhere.BoxId, out var name) ? name : elsewhere.BoxId)}\"";
            return $"{where}, slot {elsewhere.BoxSlot + 1}";
        }

        foreach (var save in saves)
        {
            var found = hashes.SelectMany(hash => save.Pkms.GetDtosByIdBase(hash).Values).FirstOrDefault();
            if (found != null || hashes.Any(hash => !save.Pkms.GetDtosByIdBase(hash).IsEmpty))
            {
                Log.Debug($"Trades: {boxId}/{boxSlot} has a copy in save {save.Save.Id} {save.Save.Metadata.FilePath}"
                    + $" box={found?.BoxId} slot={found?.BoxSlot} hashes={string.Join(",", hashes)}");
                return $"save {save.Save.Version} ({save.Save.OT}), box {found?.BoxId + 1}, slot {found?.BoxSlot + 1}";
            }
        }

        return null;
    }
}
