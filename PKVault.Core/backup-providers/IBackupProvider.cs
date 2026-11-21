namespace PKVault.Core.BackupProviders;

/// <summary>
/// A backup zip made in memory by <see cref="BackupService"/> right after a successful save
/// (the local "backup_before_save" holds the state before the save, so it is not pushed).
/// </summary>
public record BackupArtifact(
    string Name,
    DateTime CreatedAt,
    byte[] Content,
    string Sha256
)
{
    public long Size => Content.LongLength;
}

/// <summary>
/// An extra destination for backups (cloud server, mirror folder...).
/// The local backup folder stays the source of truth; providers receive a snapshot of the
/// saved state in the background, so a slow or failing provider never blocks or breaks "Save".
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
