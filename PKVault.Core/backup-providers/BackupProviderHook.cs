using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PKVault.Core.BackupProviders;

/// <summary>
/// The calls made from the upstream <see cref="BackupService"/>.
/// They do nothing when no dispatcher or no enabled provider exists (tests, hosts without providers),
/// and never throw: a provider problem must not break a save or a local backup.
/// </summary>
public static class BackupProviderHook
{
    public const string AfterSaveBackupName = "after_save";

    /// <summary>
    /// Called by PrepareBackupThenRun once the save succeeded:
    /// builds an in-memory snapshot of the saved state, which reaches <see cref="NotifyCreated"/>.
    /// </summary>
    public static async Task AfterSave(IServiceProvider sp, BackupService backupService)
    {
        try
        {
            var dispatcher = sp.GetService<BackupProviderDispatcher>();
            if (dispatcher == null || !dispatcher.AnyEnabled())
                return;

            await backupService.CreateBackup(AfterSaveBackupName, new(), writeLocal: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Backup providers: could not build the after-save snapshot");
        }
    }

    /// <summary>Called by CreateBackup for in-memory snapshots (writeLocal = false).</summary>
    public static void NotifyCreated(IServiceProvider sp, string name, DateTime createdAt, byte[] content)
    {
        try
        {
            var dispatcher = sp.GetService<BackupProviderDispatcher>();
            if (dispatcher == null || !dispatcher.HasProviders)
                return;

            dispatcher.Enqueue(new BackupArtifact(
                Name: name,
                // BackupService uses TimeProvider.GetUtcNow().DateTime: UTC with an unspecified kind
                CreatedAt: DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
                Content: content,
                Sha256: Convert.ToHexStringLower(SHA256.HashData(content))
            ));
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Backup providers: could not queue {name}");
        }
    }
}
