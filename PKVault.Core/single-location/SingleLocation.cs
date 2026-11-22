using PKHeX.Core;

namespace PKVault.Core;

/// <summary>
/// Fork rule: a pokémon is either in a save or in the storage, never both.
/// - no "attached" move and no "detach" (they leave two copies of the same pokémon);
/// - moving to a save takes every variant out of the storage;
/// - only to a save of the same generation as the pokémon, or a newer one (no data loss).
/// Links made before this rule are resolved by <see cref="SingleLocationService"/>.
/// </summary>
public static class SingleLocation
{
    /// <summary>Always on in the fork. Kept as a flag so upstream code paths stay readable.</summary>
    public static bool Enabled { get; } = true;

    public static void ThrowIfAttached(bool attached)
    {
        if (Enabled && attached)
            throw new ArgumentException("A pokémon is either in a save or in the storage: attached moves and detach are disabled");
    }

    /// <summary>Generation of the pokémon in the storage: the format of its main variant.</summary>
    public static byte GetGeneration(IEnumerable<PkmVariantEntity> slotVariants)
    {
        var variants = slotVariants.ToList();
        var main = variants.FirstOrDefault(v => v.IsMain) ?? variants.First();
        return ContextGeneration(main.Context);
    }

    public static byte ContextGeneration(EntityContext context) => context switch
    {
        EntityContext.Gen7b => 7,
        EntityContext.Gen8a or EntityContext.Gen8b => 8,
        EntityContext.Gen9a => 9,
        _ => (byte)context,
    };

    public static void ThrowIfOlderGeneration(IEnumerable<PkmVariantEntity> slotVariants, byte saveGeneration)
    {
        if (!Enabled)
            return;

        var generation = GetGeneration(slotVariants);
        if (saveGeneration < generation)
            throw new ArgumentException($"Cannot move a generation {generation} pokémon to a generation {saveGeneration} save");
    }
}
