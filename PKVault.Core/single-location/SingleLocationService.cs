using Microsoft.Extensions.DependencyInjection;
using PKVault.Core.Cloud;
using Serilog;

namespace PKVault.Core;

public enum SingleLocationError
{
    /// <summary>Unsaved actions: save or discard them first.</summary>
    PENDING_CHANGES,
    FAILED,
}

/// <param name="InSaveCount">Storage pokémon that are also in a save (attached or copied): the save keeps them.</param>
/// <param name="LinkOnlyCount">Attached to a save that is missing or no longer has them: the storage keeps them.</param>
public record SingleLocationStatusDTO(int InSaveCount, int LinkOnlyCount);

public record SingleLocationResultDTO(bool Success, SingleLocationError? Error, int Removed);

/// <summary>
/// Finds and resolves pokémon that are both in a save and in the storage (links and copies made before the fork rule).
/// </summary>
public class SingleLocationService(IServiceProvider sp, CloudSyncService sync)
{
    public async Task<SingleLocationStatusDTO> GetStatus()
    {
        var conflicts = await ResolveSingleLocationAction.FindConflicts(
            sp.GetRequiredService<IPkmVariantLoader>(), sp.GetRequiredService<ISavesLoadersService>()
        );
        return new(conflicts.Count(c => c.InSave), conflicts.Count(c => !c.InSave));
    }

    public async Task<SingleLocationResultDTO> Resolve(CancellationToken ct)
    {
        return await sync.RunExclusive(async () =>
        {
            if (!sp.GetRequiredService<ISessionService>().HasEmptyActionList())
                return new SingleLocationResultDTO(false, SingleLocationError.PENDING_CHANGES, 0);

            var status = await GetStatus();
            if (status.InSaveCount == 0 && status.LinkOnlyCount == 0)
                return new SingleLocationResultDTO(true, null, 0);

            var actions = sp.GetRequiredService<ActionService>();
            try
            {
                await actions.AddForkAction(
                    scope => scope.ServiceProvider.GetRequiredService<ResolveSingleLocationAction>(),
                    new ResolveSingleLocationActionInput()
                );
                await actions.Save();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Single location: could not resolve links and copies");
                if (!sp.GetRequiredService<ISessionService>().HasEmptyActionList())
                    await actions.RemoveDataActionsAndReset(0);
                return new SingleLocationResultDTO(false, SingleLocationError.FAILED, 0);
            }

            Log.Information($"Single location: removed {status.InSaveCount} storage pokémon kept in saves, unlinked {status.LinkOnlyCount}");
            return new SingleLocationResultDTO(true, null, status.InSaveCount);
        }, ct);
    }
}
