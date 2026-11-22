using Microsoft.Extensions.DependencyInjection;
using PKHeX.Core;
using PKVault.Core.Cloud;
using Serilog;

namespace PKVault.Core.Trades;

/// <summary>
/// Trades with custody: an offered pokémon leaves this storage and waits on the server
/// until the trade ends; pokémon received (or given back) are stored here by <see cref="ClaimPending"/>.
/// Every change runs with the sync lock (<see cref="CloudSyncService.RunExclusive"/>) and only with no unsaved actions,
/// because each one ends with a "Save".
/// </summary>
public sealed class TradeService(
    CloudStore store, CloudAuthService auth, PocketBaseClient client, CloudSyncService sync, IServiceProvider sp
)
{
    private int claimRevision;

    public int ClaimRevision => Volatile.Read(ref claimRevision);

    /// <summary>Overridable for tests: storage changes (default: session actions + Save).</summary>
    public ITradeLocalStorage? LocalStorage { get; set; }

    private ITradeLocalStorage Local => LocalStorage ??= new TradeLocalStorage(sp);

    public async Task<TradesStatusDTO> GetStatus(CancellationToken ct)
    {
        var config = store.Get();
        if (!config.IsLoggedIn)
            return new(false, null, [], [], 0, ClaimRevision, null, null);

        try
        {
            var token = await auth.GetValidToken(ct);
            if (token == null)
                return new(false, null, [], [], 0, ClaimRevision, TradeError.NOT_LOGGED_IN, null);

            var usersTask = client.ListUsers(config.ServerUrl, token, ct);
            var tradesTask = client.ListTrades(config.ServerUrl, token, ct);
            var toClaimTask = client.ListPokemonToClaim(config.ServerUrl, token, config.UserId!, ct);
            await Task.WhenAll(usersTask, tradesTask, toClaimTask);

            var users = usersTask.Result;
            var names = users.ToDictionary(u => u.Id, u => u.Name);
            string Name(string id) => names.TryGetValue(id, out var name) ? name : "";

            var trades = tradesTask.Result.Select(t => new TradeDTO(
                Id: t.Id,
                Status: t.Status,
                Kind: t.Kind,
                FromUserId: t.From,
                FromName: Name(t.From),
                ToUserId: t.To,
                ToName: Name(t.To),
                IsSender: t.From == config.UserId,
                Message: t.Message,
                CounterMessage: t.CounterMessage,
                Offered: t.Offered,
                Countered: t.Countered,
                Created: t.Created,
                ExpiresAt: t.ExpiresAt,
                ClosedAt: t.ClosedAt
            )).ToList();

            var toClaim = toClaimTask.Result.Count;
            if (toClaim > 0)
                sync.Trigger(force: true);  // the sync stores them (it waits for unsaved actions)

            return new(
                LoggedIn: true,
                UserId: config.UserId,
                Trades: trades,
                Friends: [.. users.Where(u => u.Id != config.UserId)],
                ToClaimCount: toClaim,
                ClaimRevision: ClaimRevision,
                Error: null,
                ErrorDetail: null
            );
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Trades: status failed");
            return new(true, config.UserId, [], [], 0, ClaimRevision, TradeError.SERVER, ex.Message);
        }
    }

    public async Task<List<PokemonHistoryEntryDTO>> GetHistory(string pkmVariantId, CancellationToken ct)
    {
        var config = store.Get();
        if (!config.IsLoggedIn)
            return [];

        try
        {
            var fingerprint = await Local.GetFingerprint(pkmVariantId);
            var token = await auth.GetValidToken(ct);
            if (fingerprint == null || token == null)
                return [];

            var usersTask = client.ListUsers(config.ServerUrl, token, ct);
            var historyTask = client.ListHistory(config.ServerUrl, token, fingerprint, ct);
            await Task.WhenAll(usersTask, historyTask);

            var names = usersTask.Result.ToDictionary(u => u.Id, u => u.Name);
            string Name(string? id) => id != null && names.TryGetValue(id, out var name) ? name : "";

            return [.. historyTask.Result.Select(h => new PokemonHistoryEntryDTO(Name(h.FromUser), Name(h.ToUser), h.Created))];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Trades: history failed");
            return [];
        }
    }

    public Task<TradeActionResultDTO> Offer(TradeOfferRequestDTO request, CancellationToken ct) =>
        Guarded(async (config, token) =>
        {
            var outgoing = await Local.PrepareOutgoing(request.PkmVariantId);
            var kind = request.Gift ? TradeKind.GIFT : TradeKind.TRADE;
            var trade = await client.CreateTradeOffer(config.ServerUrl, token, request.ToUserId, request.Message, kind, outgoing.Upload, ct);
            await RemoveOutgoing(config, token, outgoing, trade.Id, rollbackAction: "cancel", ct);
        }, ct);

    public Task<TradeActionResultDTO> Counter(string tradeId, TradeCounterRequestDTO request, CancellationToken ct) =>
        Guarded(async (config, token) =>
        {
            var outgoing = await Local.PrepareOutgoing(request.PkmVariantId);
            var trade = await client.CounterTrade(config.ServerUrl, token, tradeId, request.Message, outgoing.Upload, ct);
            await RemoveOutgoing(config, token, outgoing, trade.Id, rollbackAction: "decline", ct);
        }, ct);

    public Task<TradeActionResultDTO> Accept(string tradeId, CancellationToken ct) => SimpleAction(tradeId, "accept", ct);

    public Task<TradeActionResultDTO> Decline(string tradeId, CancellationToken ct) => SimpleAction(tradeId, "decline", ct);

    public Task<TradeActionResultDTO> Cancel(string tradeId, CancellationToken ct) => SimpleAction(tradeId, "cancel", ct);

    private Task<TradeActionResultDTO> SimpleAction(string tradeId, string action, CancellationToken ct) =>
        Guarded(async (config, token) =>
        {
            await client.TradeAction(config.ServerUrl, token, tradeId, action, ct);
            // pokémon received or given back: store them right away
            await ClaimPendingInner(config, token, ct);
        }, ct);

    /// <summary>
    /// Stores here every pokémon waiting on the server for this user, then tells the server.
    /// Called by the sync (lock held). Never throws.
    /// </summary>
    public async Task<int> ClaimPending(string token, CancellationToken ct)
    {
        try
        {
            var config = store.Get();
            return config.IsLoggedIn ? await ClaimPendingInner(config, token, ct) : 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Trades: storing received pokémon failed, will retry");
            return 0;
        }
    }

    private async Task<int> ClaimPendingInner(CloudConfig config, string token, CancellationToken ct)
    {
        if (!Local.HasNoPendingActions())
            return 0;

        var pending = await client.ListPokemonToClaim(config.ServerUrl, token, config.UserId!, ct);
        if (pending.Count == 0)
            return 0;

        // 1. store the new ones in one save
        var stored = config.StoredTradePokemonIds.ToHashSet();
        var toStore = new List<(PocketBaseTradePokemonRecord Record, byte[] Content)>();
        foreach (var record in pending.Where(p => !stored.Contains(p.Id)))
        {
            toStore.Add((record, await client.DownloadTradePokemon(config.ServerUrl, token, record, ct)));
        }

        var storedNow = toStore.Count == 0 ? [] : await Local.StoreIncoming(toStore.Select(p => (p.Record.Id, p.Record.Format, p.Content)).ToList());
        if (storedNow.Count > 0)
        {
            store.Update(c => c with { StoredTradePokemonIds = [.. c.StoredTradePokemonIds.Union(storedNow)] });
            Interlocked.Increment(ref claimRevision);
            Log.Information($"Trades: stored {storedNow.Count} received pokémon");
        }

        // 2. tell the server, it deletes its copy
        var done = new List<string>();
        foreach (var id in store.Get().StoredTradePokemonIds.Intersect(pending.Select(p => p.Id)))
        {
            await client.MarkPokemonClaimed(config.ServerUrl, token, id, ct);
            done.Add(id);
        }
        // forget ids the server does not list anymore (claimed earlier)
        var pendingIds = pending.Select(p => p.Id).ToHashSet();
        store.Update(c => c with { StoredTradePokemonIds = [.. c.StoredTradePokemonIds.Where(id => pendingIds.Contains(id) && !done.Contains(id))] });

        return storedNow.Count;
    }

    /// <summary>
    /// The pokémon is on the server now: remove it from this storage.
    /// If that fails, the trade is undone on the server and its copy dropped, so the pokémon is never duplicated.
    /// </summary>
    private async Task RemoveOutgoing(CloudConfig config, string token, TradeOutgoing outgoing, string tradeId, string rollbackAction, CancellationToken ct)
    {
        try
        {
            await Local.RemoveOutgoing(outgoing.VariantId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Trades: could not remove the offered pokémon locally, undoing trade {tradeId}");
            try
            {
                await Local.DiscardPendingActions();
                await client.TradeAction(config.ServerUrl, token, tradeId, rollbackAction, CancellationToken.None);
                // our pokémon is back as TO_CLAIM: it is still here, drop the server copy
                var mine = await client.ListPokemonToClaim(config.ServerUrl, token, config.UserId!, CancellationToken.None);
                foreach (var record in mine.Where(p => p.Trade == tradeId))
                    await client.MarkPokemonClaimed(config.ServerUrl, token, record.Id, CancellationToken.None);
            }
            catch (Exception rollbackEx)
            {
                Log.Error(rollbackEx, $"Trades: undo of trade {tradeId} failed");
            }
            throw new TradeException(TradeError.CANNOT_TRADE, ex.Message);
        }
    }

    private async Task<TradeActionResultDTO> Guarded(Func<CloudConfig, string, Task> work, CancellationToken ct)
    {
        var config = store.Get();
        if (!config.IsLoggedIn)
            return new(false, TradeError.NOT_LOGGED_IN, null);

        try
        {
            return await sync.RunExclusive(async () =>
            {
                if (!Local.HasNoPendingActions())
                    return new TradeActionResultDTO(false, TradeError.PENDING_CHANGES, null);

                var token = await auth.GetValidToken(ct);
                if (token == null)
                    return new TradeActionResultDTO(false, TradeError.NOT_LOGGED_IN, null);

                await work(store.Get(), token);
                return new TradeActionResultDTO(true, null, null);
            }, ct);
        }
        catch (TradeException ex)
        {
            return new(false, ex.Error, ex.Message);
        }
        catch (PocketBaseException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
        {
            return new(false, TradeError.NOT_FOUND, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Trades: action failed");
            return new(false, TradeError.SERVER, ex.Message);
        }
    }
}

public record TradeOutgoing(string VariantId, TradePokemonUpload Upload);

/// <summary>Storage side of trades.</summary>
public interface ITradeLocalStorage
{
    bool HasNoPendingActions();
    Task<string?> GetFingerprint(string pkmVariantId);
    /// <exception cref="TradeException">pokémon not found or not tradable</exception>
    Task<TradeOutgoing> PrepareOutgoing(string pkmVariantId);
    /// <summary>Deletes the pokémon (all its variants) and saves.</summary>
    Task RemoveOutgoing(string pkmVariantId);
    Task DiscardPendingActions();
    /// <summary>Adds the pokémon to the storage and saves. Returns the ids actually stored.</summary>
    Task<List<string>> StoreIncoming(List<(string Id, string Format, byte[] Content)> pokemons);
}

public sealed class TradeLocalStorage(IServiceProvider sp) : ITradeLocalStorage
{
    public bool HasNoPendingActions()
    {
        try
        {
            return sp.GetRequiredService<ISessionService>().HasEmptyActionList();
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<string?> GetFingerprint(string pkmVariantId)
    {
        using var scope = sp.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IPkmVariantLoader>();
        return (await loader.GetEntity(pkmVariantId))?.Hash;
    }

    public async Task<TradeOutgoing> PrepareOutgoing(string pkmVariantId)
    {
        using var scope = sp.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IPkmVariantLoader>();

        var entity = await loader.GetEntity(pkmVariantId)
            ?? throw new TradeException(TradeError.NOT_FOUND, $"Pokémon not found: {pkmVariantId}");

        var slotVariants = (await loader.GetEntitiesByBox(entity.BoxId, entity.BoxSlot)).Values.ToList();
        if (slotVariants.Count == 0)
            slotVariants = [entity];

        if (slotVariants.Any(v => v.IsExternal))
            throw new TradeException(TradeError.CANNOT_TRADE, "External pokémon cannot be traded");
        if (slotVariants.Any(v => v.AttachedSaveId != null))
            throw new TradeException(TradeError.CANNOT_TRADE, "Pokémon attached to a save: detach it first");

        // the same pokémon somewhere else (a save after "detach", or another storage slot): trading it would duplicate it
        var hashes = slotVariants.Select(v => v.Hash).Where(h => !string.IsNullOrEmpty(h)).ToHashSet();
        var elsewhereInStorage = (await loader.GetAllEntities()).Values
            .Any(v => hashes.Contains(v.Hash) && (v.BoxId != entity.BoxId || v.BoxSlot != entity.BoxSlot));
        var inSave = sp.GetService<ISavesLoadersService>()?.GetAllLoaders()
            .Any(save => hashes.Any(hash => !save.Pkms.GetDtosByIdBase(hash).IsEmpty)) ?? false;
        if (elsewhereInStorage || inSave)
            throw new TradeException(TradeError.DUPLICATE, "Another copy of this pokémon exists in a save or in the storage");

        var main = slotVariants.FirstOrDefault(v => v.IsMain) ?? entity;
        var pkm = await loader.GetPKM(main);
        if (!pkm.IsEnabled || pkm.IsEgg || pkm.Species == 0)
            throw new TradeException(TradeError.CANNOT_TRADE, "This pokémon cannot be traded");

        return new(main.Id, new(
            Content: pkm.GetDecryptedDataParty(),
            Format: pkm.Extension,
            Fingerprint: main.Hash,
            Species: pkm.Species,
            Form: pkm.Form,
            Nickname: pkm.Nickname,
            Level: pkm.CurrentLevel,
            Shiny: pkm.IsShiny,
            Gender: (int)pkm.Gender,
            OtName: pkm.OriginalTrainerName,
            Tid: (int)pkm.DisplayTID,
            Version: (int)pkm.Version
        ));
    }

    public async Task RemoveOutgoing(string pkmVariantId)
    {
        var actions = sp.GetRequiredService<ActionService>();
        await actions.MainPkmVariantsDelete(new([pkmVariantId], deleteAllRelatedVariants: true));
        await actions.Save();
    }

    public async Task DiscardPendingActions()
    {
        var session = sp.GetRequiredService<ISessionService>();
        if (!session.HasEmptyActionList())
            await sp.GetRequiredService<ActionService>().RemoveDataActionsAndReset(0);
    }

    public async Task<List<string>> StoreIncoming(List<(string Id, string Format, byte[] Content)> pokemons)
    {
        var actions = sp.GetRequiredService<ActionService>();
        var added = new List<string>();

        foreach (var (id, format, content) in pokemons)
        {
            var pkm = Parse(content, format);
            if (pkm == null)
            {
                Log.Warning($"Trades: received pokémon {id} has an unreadable file ({format}), left on the server");
                continue;
            }
            try
            {
                await actions.AddForkAction(
                    scope => scope.ServiceProvider.GetRequiredService<TradeReceivePkmAction>(),
                    new TradeReceivePkmActionInput(pkm)
                );
                added.Add(id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, $"Trades: could not store received pokémon {id}");
            }
        }

        if (added.Count > 0)
        {
            try
            {
                await actions.Save();
            }
            catch (Exception)
            {
                // nothing stored: leave them on the server for the next try
                await actions.RemoveDataActionsAndReset(0);
                throw;
            }
        }

        return added;
    }

    public static ImmutablePKM? Parse(byte[] content, string format)
    {
        var ext = "." + format.TrimStart('.');
        if (!FileUtil.TryGetPKM(content, out var pk, ext, new SimpleTrainerInfo()) || pk == null)
            return null;

        var pkm = new ImmutablePKM(pk);
        return pkm.IsEnabled && pkm.Species > 0 ? pkm : null;
    }
}
