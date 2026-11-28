using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using Serilog;

namespace PKVault.Core.Cloud;

/// <summary>
/// Cloud account data kept on this computer, in config/cloud.json (next to pkvault.json).
/// The token is a PocketBase auth token: it only gives access to this user's own records.
/// </summary>
public record CloudConfig
{
    public string ServerUrl { get; init; } = CloudStore.DefaultServerUrl;
    public string? Token { get; init; }
    public string? Provider { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? UserEmail { get; init; }
    /// <summary>Last vault_backups record pushed from this computer: parent of the next one.</summary>
    public string? LastBackupRecordId { get; init; }
    /// <summary>Time of a local save whose snapshot is not on the server yet (offline, upload failed...).</summary>
    public DateTime? PendingLocalSaveAt { get; init; }
    /// <summary>Trade pokémon already stored here, but not yet marked as claimed on the server (never store them twice).</summary>
    public IReadOnlyList<string> StoredTradePokemonIds { get; init; } = [];
    /// <summary>Last known approval status of the account (checked at each sync).</summary>
    public CloudAccountStatus AccountStatus { get; init; } = CloudAccountStatus.APPROVED;
    /// <summary>The server refused the saved session (too long without opening the app, or everyone logged out): log in again.</summary>
    public bool SessionExpired { get; init; }

    public bool IsLoggedIn => !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(UserId);

    public bool IsApproved => IsLoggedIn && AccountStatus == CloudAccountStatus.APPROVED;

    public CloudConfig LoggedOut() => new() { ServerUrl = ServerUrl, StoredTradePokemonIds = StoredTradePokemonIds };

    /// <summary>
    /// Session refused by the server: only the token goes. The account stays known, so the app can say
    /// "log in again" and, with the same account, continue the backup chain and pending saves.
    /// </summary>
    public CloudConfig Expired() => this with { Token = null, SessionExpired = true };
}

public class CloudStore(IFileSystem fileSystem, string? filePath = null)
{
    public const string DefaultServerUrl = "https://pokehome-johann.duckdns.org";

    public static string DefaultFilePath => MatcherUtil.NormalizePath(
        Path.Combine(Path.GetDirectoryName(SettingsService.FilePath)!, "cloud.json")
    );

    private readonly string path = filePath ?? DefaultFilePath;
    private readonly Lock gate = new();
    private CloudConfig? cached;

    public CloudConfig Get()
    {
        lock (gate)
        {
            return cached ??= Read();
        }
    }

    public CloudConfig Update(Func<CloudConfig, CloudConfig> change)
    {
        lock (gate)
        {
            var next = change(cached ??= Read());
            Write(next);
            cached = next;
            return next;
        }
    }

    private CloudConfig Read()
    {
        try
        {
            if (!fileSystem.File.Exists(path))
                return new();

            using var doc = JsonDocument.Parse(fileSystem.File.ReadAllText(path));
            var root = doc.RootElement;
            string? Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var serverUrl = Str("serverUrl");
            return new()
            {
                ServerUrl = string.IsNullOrWhiteSpace(serverUrl) ? DefaultServerUrl : serverUrl.TrimEnd('/'),
                Token = Str("token"),
                Provider = Str("provider"),
                UserId = Str("userId"),
                UserName = Str("userName"),
                UserEmail = Str("userEmail"),
                LastBackupRecordId = Str("lastBackupRecordId"),
                PendingLocalSaveAt = DateTime.TryParse(Str("pendingLocalSaveAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var pending)
                    ? pending.ToUniversalTime()
                    : null,
                AccountStatus = PocketBaseClient.ParseAccountStatus(Str("accountStatus")),
                SessionExpired = root.TryGetProperty("sessionExpired", out var expired) && expired.ValueKind == JsonValueKind.True,
                StoredTradePokemonIds = root.TryGetProperty("storedTradePokemonIds", out var stored) && stored.ValueKind == JsonValueKind.Array
                    ? [.. stored.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!)]
                    : [],
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Cloud: could not read {path}, starting logged out");
            return new();
        }
    }

    private void Write(CloudConfig config)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("serverUrl", config.ServerUrl);
            void Opt(string name, string? value)
            {
                if (value != null)
                    writer.WriteString(name, value);
            }
            Opt("token", config.Token);
            Opt("provider", config.Provider);
            Opt("userId", config.UserId);
            Opt("userName", config.UserName);
            Opt("userEmail", config.UserEmail);
            Opt("lastBackupRecordId", config.LastBackupRecordId);
            if (config.AccountStatus != CloudAccountStatus.APPROVED)
                writer.WriteString("accountStatus", config.AccountStatus.ToString());
            if (config.SessionExpired)
                writer.WriteBoolean("sessionExpired", true);
            Opt("pendingLocalSaveAt", config.PendingLocalSaveAt?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            if (config.StoredTradePokemonIds.Count > 0)
            {
                writer.WriteStartArray("storedTradePokemonIds");
                foreach (var id in config.StoredTradePokemonIds)
                    writer.WriteStringValue(id);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            fileSystem.Directory.CreateDirectory(dir);
        fileSystem.File.WriteAllText(path, Encoding.UTF8.GetString(stream.ToArray()));
    }
}
