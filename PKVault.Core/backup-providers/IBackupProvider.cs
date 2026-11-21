namespace PKVault.Core.BackupProviders;

/// <summary>
/// A backup zip that was just written locally by <see cref="BackupService"/>.
/// </summary>
public record BackupArtifact(
    string FilePath,
    string Name,
    DateTime CreatedAt,
    string Sha256,
    long Size
);

/// <summary>
/// An extra destination for backups (cloud server, mirror folder...).
/// The local backup folder stays the source of truth; providers receive a copy
/// in the background, so a slow or failing provider never blocks or breaks "Save".
/// </summary>
public interface IBackupProvider
{
    /// <summary>Stable id, used in logs and status (e.g. "pocketbase").</summary>
    string Id { get; }

    /// <summary>False when the provider is not configured (e.g. user not logged in): it is skipped.</summary>
    bool IsEnabled();

    /// <summary>Sends the backup. Throwing means failure; the dispatcher retries.</summary>
    Task Push(BackupArtifact artifact, CancellationToken cancellationToken);
}
