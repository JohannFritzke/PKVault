using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PKVault.Core.BackupProviders;

/// <summary>
/// The single call made from the upstream <see cref="BackupService"/> after a backup zip is written.
/// Does nothing when no dispatcher is registered (tests, hosts without providers),
/// and never throws: a provider problem must not break a local backup.
/// </summary>
public static class BackupProviderHook
{
    public static void NotifyCreated(IServiceProvider sp, string filePath, string name, DateTime createdAt, byte[] content)
    {
        try
        {
            var dispatcher = sp.GetService<BackupProviderDispatcher>();
            if (dispatcher == null || !dispatcher.HasProviders)
                return;

            dispatcher.Enqueue(new BackupArtifact(
                FilePath: filePath,
                Name: name,
                CreatedAt: createdAt,
                Sha256: Convert.ToHexStringLower(SHA256.HashData(content)),
                Size: content.LongLength
            ));
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Backup providers: could not queue {filePath}");
        }
    }
}
