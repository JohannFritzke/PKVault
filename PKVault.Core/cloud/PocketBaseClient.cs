using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PKVault.Core.Cloud;

public record PocketBaseAuth(string Token, string UserId, string? Name, string? Email);

public record PocketBaseOAuthProvider(string Name, string AuthUrl, string CodeVerifier);

public record PocketBaseOAuthResult(string? State, string? Code, string? Error);

public class PocketBaseException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>
/// Minimal PocketBase REST client (users auth + vault_backups).
/// JSON is read with JsonDocument and written with Utf8JsonWriter: no reflection, safe with trimming.
/// </summary>
public class PocketBaseClient(HttpClient http)
{
    public const string UsersCollection = "users";
    public const string BackupsCollection = "vault_backups";
    public const string OAuthTopic = "@oauth2";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(10);

    public static string RedirectUrl(string serverUrl) => $"{serverUrl.TrimEnd('/')}/api/oauth2-redirect";

    public async Task<PocketBaseOAuthProvider> GetOAuthProvider(string serverUrl, string provider, CancellationToken ct)
    {
        using var doc = await SendJson(HttpMethod.Get, $"{serverUrl}/api/collections/{UsersCollection}/auth-methods", null, null, ct);

        if (doc.RootElement.TryGetProperty("oauth2", out var oauth2)
            && oauth2.TryGetProperty("providers", out var providers))
        {
            foreach (var p in providers.EnumerateArray())
            {
                if (p.GetProperty("name").GetString() == provider)
                    return new(provider, p.GetProperty("authURL").GetString()!, p.GetProperty("codeVerifier").GetString()!);
            }
        }

        throw new PocketBaseException(HttpStatusCode.NotFound, $"Login provider '{provider}' is not enabled on the server");
    }

    /// <summary>
    /// Final authorize url: the provider authURL with our redirect url, and `state` replaced by the
    /// realtime client id so that /api/oauth2-redirect sends the code back to our realtime connection.
    /// </summary>
    public static string BuildAuthUrl(PocketBaseOAuthProvider provider, string redirectUrl, string realtimeClientId)
    {
        var url = provider.AuthUrl + Uri.EscapeDataString(redirectUrl);
        var queryStart = url.IndexOf('?');
        if (queryStart < 0)
            return url;

        var parts = url[ (queryStart + 1).. ].Split('&');
        var hasState = false;
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[ i ].StartsWith("state=", StringComparison.Ordinal))
            {
                parts[ i ] = "state=" + Uri.EscapeDataString(realtimeClientId);
                hasState = true;
            }
        }

        var query = string.Join('&', parts);
        if (!hasState)
            query += "&state=" + Uri.EscapeDataString(realtimeClientId);

        return url[ ..(queryStart + 1) ] + query;
    }

    /// <summary>Opens the realtime (SSE) stream and returns once the server gave a client id.</summary>
    public async Task<PocketBaseRealtime> ConnectRealtime(string serverUrl, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{serverUrl}/api/realtime");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new PocketBaseException(response.StatusCode, $"Realtime connection refused ({(int)response.StatusCode})");
        }

        var realtime = new PocketBaseRealtime(response, await response.Content.ReadAsStreamAsync(ct));
        try
        {
            var connect = await realtime.ReadEvent(ct) ?? throw new IOException("Realtime stream closed before PB_CONNECT");
            if (connect.Event != "PB_CONNECT")
                throw new IOException($"Unexpected realtime event {connect.Event}");

            using var data = JsonDocument.Parse(connect.Data);
            realtime.ClientId = data.RootElement.GetProperty("clientId").GetString()!;
            return realtime;
        }
        catch
        {
            realtime.Dispose();
            throw;
        }
    }

    public async Task Subscribe(string serverUrl, string clientId, string topic, CancellationToken ct)
    {
        var body = Json(w =>
        {
            w.WriteString("clientId", clientId);
            w.WriteStartArray("subscriptions");
            w.WriteStringValue(topic);
            w.WriteEndArray();
        });
        using var _ = await SendJson(HttpMethod.Post, $"{serverUrl}/api/realtime", body, null, ct, allowEmpty: true);
    }

    public async Task<PocketBaseAuth> AuthWithOAuth2Code(
        string serverUrl, string provider, string code, string codeVerifier, string redirectUrl, CancellationToken ct
    )
    {
        var body = Json(w =>
        {
            w.WriteString("provider", provider);
            w.WriteString("code", code);
            w.WriteString("codeVerifier", codeVerifier);
            w.WriteString("redirectURL", redirectUrl);
        });
        using var doc = await SendJson(HttpMethod.Post, $"{serverUrl}/api/collections/{UsersCollection}/auth-with-oauth2", body, null, ct);
        return ReadAuth(doc.RootElement);
    }

    public async Task<PocketBaseAuth> AuthRefresh(string serverUrl, string token, CancellationToken ct)
    {
        using var doc = await SendJson(HttpMethod.Post, $"{serverUrl}/api/collections/{UsersCollection}/auth-refresh", null, token, ct);
        return ReadAuth(doc.RootElement);
    }

    public async Task<string?> FindBackupBySha256(string serverUrl, string token, string sha256, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"sha256='{sha256}'");
        using var doc = await SendJson(HttpMethod.Get,
            $"{serverUrl}/api/collections/{BackupsCollection}/records?perPage=1&skipTotal=1&fields=id&filter={filter}", null, token, ct);

        var items = doc.RootElement.GetProperty("items");
        return items.GetArrayLength() > 0 ? items[ 0 ].GetProperty("id").GetString() : null;
    }

    public async Task<bool> BackupExists(string serverUrl, string token, string recordId, CancellationToken ct)
    {
        try
        {
            using var _ = await SendJson(HttpMethod.Get,
                $"{serverUrl}/api/collections/{BackupsCollection}/records/{Uri.EscapeDataString(recordId)}?fields=id", null, token, ct);
            return true;
        }
        catch (PocketBaseException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<string> CreateBackup(
        string serverUrl, string token, string ownerId, string fileName, byte[] content,
        string sha256, string device, string appVersion, string? parentId, CancellationToken ct
    )
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(ownerId), "owner" },
            { new StringContent(sha256), "sha256" },
            { new StringContent(device), "device" },
            { new StringContent(appVersion), "appVersion" },
        };
        if (!string.IsNullOrEmpty(parentId))
            form.Add(new StringContent(parentId), "parent");

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", fileName);

        using var doc = await Send(HttpMethod.Post, $"{serverUrl}/api/collections/{BackupsCollection}/records", form, token, ct, UploadTimeout, allowEmpty: false);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    private static PocketBaseAuth ReadAuth(JsonElement root)
    {
        var record = root.GetProperty("record");
        string? Str(JsonElement el, string name) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() != "" ? v.GetString() : null;

        var name = Str(record, "name");
        if (name == null && root.TryGetProperty("meta", out var meta))
            name = Str(meta, "name") ?? Str(meta, "username");

        return new(
            Token: root.GetProperty("token").GetString()!,
            UserId: record.GetProperty("id").GetString()!,
            Name: name,
            Email: Str(record, "email")
        );
    }

    private static ByteArrayContent Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        var content = new ByteArrayContent(stream.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private async Task<JsonDocument> SendJson(HttpMethod method, string url, HttpContent? body, string? token, CancellationToken ct, bool allowEmpty = false)
    {
        var doc = await Send(method, url, body, token, ct, RequestTimeout, allowEmpty);
        return doc ?? JsonDocument.Parse("{}");
    }

    private async Task<JsonDocument?> Send(HttpMethod method, string url, HttpContent? body, string? token, CancellationToken ct, TimeSpan timeout, bool allowEmpty)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        using var request = new HttpRequestMessage(method, url) { Content = body };
        if (token != null)
            request.Headers.TryAddWithoutValidation("Authorization", token);

        using var response = await http.SendAsync(request, timeoutCts.Token);
        var text = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
            throw new PocketBaseException(response.StatusCode, ErrorMessage(response.StatusCode, text));

        if (string.IsNullOrWhiteSpace(text))
            return allowEmpty ? null : throw new IOException($"Empty response from {method} {url}");

        return JsonDocument.Parse(text);
    }

    private static string ErrorMessage(HttpStatusCode status, string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
            var details = new StringBuilder();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in data.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Object && field.Value.TryGetProperty("message", out var fm))
                        details.Append($" {field.Name}: {fm.GetString()}.");
                }
            }
            return $"Server error {(int)status}: {message}{details}";
        }
        catch (JsonException)
        {
            return $"Server error {(int)status}";
        }
    }
}

/// <summary>An open realtime stream. Disposing it closes the connection.</summary>
public sealed class PocketBaseRealtime(HttpResponseMessage response, Stream stream) : IDisposable
{
    private readonly StreamReader reader = new(stream, Encoding.UTF8);

    public string ClientId { get; internal set; } = "";

    public record SseEvent(string Event, string Data);

    /// <summary>Next server-sent event, or null when the stream ends.</summary>
    public async Task<SseEvent?> ReadEvent(CancellationToken ct)
    {
        string? eventName = null;
        var data = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
                return null;

            if (line.Length == 0)
            {
                if (eventName != null || data.Length > 0)
                    return new(eventName ?? "message", data.ToString());
                continue;
            }

            if (line.StartsWith(':'))
                continue;

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[ ..colon ];
            var value = colon < 0 ? "" : line[ (colon + 1).. ].TrimStart(' ');

            if (field == "event")
                eventName = value;
            else if (field == "data")
            {
                if (data.Length > 0)
                    data.Append('\n');
                data.Append(value);
            }
        }
    }

    /// <summary>Waits for the @oauth2 message sent by /api/oauth2-redirect.</summary>
    public async Task<PocketBaseOAuthResult> WaitForOAuthResult(CancellationToken ct)
    {
        while (true)
        {
            var e = await ReadEvent(ct) ?? throw new IOException("Realtime connection closed by the server");
            if (e.Event != PocketBaseClient.OAuthTopic)
                continue;

            using var doc = JsonDocument.Parse(e.Data);
            string? Str(string name) => doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return new(Str("state"), Str("code"), Str("error"));
        }
    }

    public void Dispose()
    {
        reader.Dispose();
        response.Dispose();
    }
}
