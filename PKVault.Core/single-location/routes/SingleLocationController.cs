namespace PKVault.Core.SingleLocationRoutes;

/// <summary>
/// api/single-location
/// </summary>
[Route("api/single-location")]
public class SingleLocationController(SingleLocationService service)
{
    /// <summary>Pokémon that are both in a save and in the storage (links and copies).</summary>
    [HttpGet("status")]
    public async Task<SingleLocationStatusDTO> Status()
    {
        return await service.GetStatus();
    }

    /// <summary>Keeps those pokémon in their save only, and saves (a backup is made first).</summary>
    [HttpPost("resolve")]
    public async Task<SingleLocationResultDTO> Resolve()
    {
        return await service.Resolve(CancellationToken.None);
    }
}
