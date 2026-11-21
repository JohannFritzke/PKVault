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
    string? BackupError
);

public record CloudLoginRequestDTO(string Provider);

public record CloudLoginStartDTO(string AuthUrl, bool BrowserOpened);
