namespace PKVault.Core;

public record ResolveSingleLocationActionInput();

/// <summary>
/// One storage slot (all its variants) that also exists in a save.
/// <paramref name="InSave"/> false: attached to a save that is not loaded or no longer has the pokémon.
/// </summary>
public record SingleLocationConflict(string BoxId, int BoxSlot, bool Attached, bool InSave, List<PkmVariantEntity> Variants);

/// <summary>
/// Puts every pokémon in one place only (fork rule, see <see cref="SingleLocation"/>):
/// - attached to a save, or with a copy in a save (after "detach"): the save keeps it, the storage slot is removed;
/// - attached to a save that is missing or no longer has it: the link is removed, the storage keeps it.
/// Save files are not changed.
/// </summary>
public class ResolveSingleLocationAction(
    IPkmVariantLoader pkmVariantLoader, ISavesLoadersService savesLoadersService
) : DataAction<ResolveSingleLocationActionInput>
{
    public static async Task<List<SingleLocationConflict>> FindConflicts(IPkmVariantLoader pkmVariantLoader, ISavesLoadersService savesLoadersService)
    {
        var saves = savesLoadersService.GetAllLoaders();

        bool inSave(string? hash) => !string.IsNullOrEmpty(hash)
            && saves.Any(save => !save.Pkms.GetDtosByIdBase(hash).IsEmpty);

        bool attachedInSave(PkmVariantEntity v) => v.AttachedSaveId is uint saveId
            && !string.IsNullOrEmpty(v.AttachedSavePkmIdBase)
            && savesLoadersService.GetLoaders(saveId)?.Pkms.GetDtosByIdBase(v.AttachedSavePkmIdBase).IsEmpty == false;

        return (await pkmVariantLoader.GetAllEntities()).Values
            .Where(v => !v.IsExternal)
            .GroupBy(v => (v.BoxId, v.BoxSlot))
            .Select(slot =>
            {
                var variants = slot.ToList();
                var attached = variants.Any(v => v.AttachedSaveId != null);
                var isInSave = variants.Any(attachedInSave) || variants.Any(v => inSave(v.Hash));
                return new SingleLocationConflict(slot.Key.BoxId, slot.Key.BoxSlot, attached, isInSave, variants);
            })
            .Where(conflict => conflict.Attached || conflict.InSave)
            .ToList();
    }

    protected override async Task<DataActionPayload> Execute(ResolveSingleLocationActionInput input, DataUpdateFlags flags)
    {
        var conflicts = await FindConflicts(pkmVariantLoader, savesLoadersService);

        foreach (var conflict in conflicts)
        {
            foreach (var variant in conflict.Variants)
            {
                if (conflict.InSave)
                {
                    await pkmVariantLoader.DeleteEntity(variant);
                }
                else if (variant.AttachedSaveId != null)
                {
                    variant.AttachedSaveId = null;
                    variant.AttachedSavePkmIdBase = null;
                    await pkmVariantLoader.UpdateEntity(variant);
                }
            }
        }

        return new(
            type: DataActionType.DELETE_PKM_VERSION,
            parameters: [$"{conflicts.Count(c => c.InSave)}", null, 0]
        );
    }
}
