namespace PKVault.Core.Trades;

public class TradeReceivePkmActionInput(ImmutablePKM pkm)
{
    public ImmutablePKM Pkm => pkm;

    // kept between replays of the action (undo / reset rebuilds the session from the inputs)
    public string? CreatedVariantId = null;
    public string? BoxId = null;
    public int BoxSlot = -1;
}

/// <summary>
/// Stores a pokémon received by trade in the first free slot of the default bank
/// (a new box is created when the bank is full).
/// </summary>
public class TradeReceivePkmAction(
    IPkmVariantLoader pkmVariantLoader, IBankLoader bankLoader, IBoxLoader boxLoader, MainCreateBoxAction mainCreateBoxAction
) : DataAction<TradeReceivePkmActionInput>
{
    protected override async Task<DataActionPayload> Execute(TradeReceivePkmActionInput input, DataUpdateFlags flags)
    {
        if (!input.Pkm.IsEnabled)
            throw new ArgumentException("Received pokémon file is invalid");

        if (input.BoxId == null)
            (input.BoxId, input.BoxSlot) = await FindFreeSlot();

        var box = await boxLoader.GetDto(input.BoxId)
            ?? throw new ArgumentException($"Box not found: {input.BoxId}");

        var entity = await pkmVariantLoader.AddEntity(new(
            Id: input.CreatedVariantId,
            Box: box,
            BoxSlot: input.BoxSlot,
            IsMain: true,
            IsExternal: false,
            AttachedSaveId: null,
            AttachedSavePkmIdBase: null,
            Context: input.Pkm.Context,
            Generation: input.Pkm.Generation,
            Pkm: input.Pkm
        ));
        input.CreatedVariantId = entity.Id;

        return new(
            type: DataActionType.MAIN_CREATE_PKM_VERSION,
            parameters: [input.Pkm.Nickname, input.Pkm.Context, input.Pkm.Species]
        );
    }

    private async Task<(string BoxId, int BoxSlot)> FindFreeSlot()
    {
        var banks = await bankLoader.GetAllEntities();
        var bank = banks.Values.Where(b => !b.IsExternal).OrderByDescending(b => b.IsDefault).FirstOrDefault()
            ?? throw new ArgumentException("No storage bank to receive the pokémon");

        var boxes = (await boxLoader.GetEntitiesByBank(bank.Id)).Values
            .Where(box => box.Type == BoxType.Box)
            .OrderBy(box => box.Order);

        foreach (var box in boxes)
        {
            var occupied = (await pkmVariantLoader.GetEntitiesByBox(box.Id)).Keys.ToHashSet();
            for (var slot = 0; slot < box.SlotCount; slot++)
            {
                if (!occupied.Contains(slot))
                    return (box.Id, slot);
            }
        }

        var created = await mainCreateBoxAction.CreateBox(new(bank.Id, 30));
        return (created.Id, 0);
    }
}
