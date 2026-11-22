using PKVault.Core.Trades;

namespace PKVault.Core.trades.routes;

/// <summary>
/// api/trade
/// </summary>
[Route("api/[controller]")]
public class TradeController(TradeService tradeService)
{
    /// <summary>Trades of the current user, friends to trade with, and pokémon waiting to be stored here.</summary>
    [HttpGet("status")]
    public async Task<TradesStatusDTO> Status()
    {
        return await tradeService.GetStatus(CancellationToken.None);
    }

    /// <summary>Owners of this pokémon, from trades.</summary>
    [HttpGet("history/{pkmVariantId}")]
    public async Task<List<PokemonHistoryEntryDTO>> History(string pkmVariantId)
    {
        return await tradeService.GetHistory(pkmVariantId, CancellationToken.None);
    }

    /// <summary>Offers a storage pokémon to a friend: it leaves the storage (a save is made) until the trade ends.</summary>
    [HttpPost("offer")]
    public async Task<TradeActionResultDTO> Offer(TradeOfferRequestDTO request)
    {
        return await tradeService.Offer(request, CancellationToken.None);
    }

    [HttpPost("{tradeId}/counter")]
    public async Task<TradeActionResultDTO> Counter(string tradeId, TradeCounterRequestDTO request)
    {
        return await tradeService.Counter(tradeId, request, CancellationToken.None);
    }

    [HttpPost("{tradeId}/accept")]
    public async Task<TradeActionResultDTO> Accept(string tradeId)
    {
        return await tradeService.Accept(tradeId, CancellationToken.None);
    }

    [HttpPost("{tradeId}/decline")]
    public async Task<TradeActionResultDTO> Decline(string tradeId)
    {
        return await tradeService.Decline(tradeId, CancellationToken.None);
    }

    [HttpPost("{tradeId}/cancel")]
    public async Task<TradeActionResultDTO> Cancel(string tradeId)
    {
        return await tradeService.Cancel(tradeId, CancellationToken.None);
    }
}
