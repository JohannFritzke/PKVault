using PKVault.Core.BackupProviders;

namespace PKVault.Core.Cloud;

public enum CloudLoginState
{
    IDLE,
    WAITING_BROWSER,
    ERROR,
}

public enum CloudLoginError
{
    /// <summary>No answer from the browser in time.</summary>
    TIMEOUT,
    /// <summary>Refused or cancelled on the Google/GitHub page.</summary>
    DENIED,
    /// <summary>Server unreachable or unexpected answer.</summary>
    SERVER,
    /// <summary>Saved session no longer valid, log in again.</summary>
    EXPIRED,
}

public enum CloudSyncState
{
    IDLE,
    CHECKING,
    PULLING,
    /// <summary>Unsaved actions: sync waits for "Save" or "Discard".</summary>
    WAITING_LOCAL_CHANGES,
    ERROR,
}

public enum CloudSyncError
{
    SERVER,
    INVALID_BACKUP,
    /// <summary>The other computer uses an absolute storage path, cannot be mapped here.</summary>
    UNSUPPORTED_PATHS,
}

public record CloudStatusDTO(
    string ServerUrl,
    bool LoggedIn,
    string? Provider,
    string? UserName,
    string? UserEmail,
    CloudLoginState LoginState,
    string? LoginProvider,
    CloudLoginError? LoginError,
    string? LoginErrorDetail,
    BackupProviderPushState? BackupState,
    DateTime? LastBackupAt,
    string? BackupError,
    CloudSyncState SyncState,
    DateTime? LastSyncAt,
    /// <summary>Increased each time data from another computer replaced local data: the app reloads.</summary>
    int SyncRevision,
    string? LastPulledDevice,
    DateTime? LastPulledBackupAt,
    int LastPulledSavesSkipped,
    CloudSyncError? SyncError,
    string? SyncErrorDetail,
    /// <summary>Increased each time pokémon received by trade were stored here: the app refreshes its storage.</summary>
    int TradeClaimRevision = 0
);

public record CloudLoginRequestDTO(string Provider);

public record CloudLoginStartDTO(string AuthUrl, bool BrowserOpened);
