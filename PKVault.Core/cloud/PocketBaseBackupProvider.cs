using PKVault.Core.BackupProviders;
using Serilog;

namespace PKVault.Core.Cloud;

/// <summary>
/// Sends after-save snapshots to the vault_backups collection of the user's PocketBase server.
/// Same content twice (same sha256) is not uploaded again.
/// </summary>
public class PocketBaseBackupProvider(CloudStore store, CloudAuthService auth, PocketBaseClient client) : IBackupProvider
{
    public const string ProviderId = "pocketbase";

    public string Id => ProviderId;

    /// <summary>Overridable for tests.</summary>
    public string Device { get; init; } = Environment.MachineName;
    public string AppVersion { get; init; } = SettingsService.GetBuildInfo().Version;

    public bool IsEnabled() => auth.IsLoggedIn;

    public async Task Push(BackupArtifact artifact, CancellationToken cancellationToken)
    {
        var token = await auth.GetValidToken(cancellationToken)
            ?? throw new InvalidOperationException("Not logged in to the cloud");

        var config = store.Get();
        var serverUrl = config.ServerUrl;

        // remembered until the upload succeeds, so sync knows this computer has newer unsent data
        if (config.PendingLocalSaveAt == null || config.PendingLocalSaveAt < artifact.CreatedAt)
            config = store.Update(c => c with { PendingLocalSaveAt = artifact.CreatedAt });

        // kept as pending (above) and sent by the sync once the account is approved
        if (!config.IsApproved)
            throw new InvalidOperationException("Cloud account waiting for approval");

        var existing = await client.FindBackupBySha256(serverUrl, token, artifact.Sha256, cancellationToken);
        if (existing != null)
        {
            Log.Information($"Cloud: backup {artifact.Sha256[ ..12 ]} already on the server ({existing})");
            RememberLast(config.UserId, existing);
            return;
        }

        var parent = config.LastBackupRecordId;
        if (parent != null && !await client.BackupExists(serverUrl, token, parent, cancellationToken))
            parent = null;      // deleted on the server

        var fileName = $"{artifact.Name}_{artifact.CreatedAt:yyyyMMdd'T'HHmmss}.zip";
        var id = await client.CreateBackup(
            serverUrl, token, config.UserId!, fileName, artifact.Content,
            artifact.Sha256, Device, AppVersion, parent, cancellationToken
        );

        Log.Information($"Cloud: backup uploaded ({id}, {artifact.Size} bytes, parent={parent ?? "none"})");
        RememberLast(config.UserId, id);
    }

    private void RememberLast(string? userId, string recordId)
    {
        store.Update(c => c.UserId == userId ? c with { LastBackupRecordId = recordId, PendingLocalSaveAt = null } : c);
    }
}
