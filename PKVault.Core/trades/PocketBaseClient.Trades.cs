using System.Net.Http.Headers;
using System.Text.Json;
using PKVault.Core.Trades;

namespace PKVault.Core.Cloud;

/// <summary>
/// Trade calls (phase 4). Every change goes through the routes of deploy/oracle/pb_hooks/trades.pb.js,
/// clients only read the trades, trade_pokemon and pokemon_history collections.
/// </summary>
public partial class PocketBaseClient
{
    public const string TradesCollection = "trades";
    public const string TradePokemonCollection = "trade_pokemon";
    public const string PokemonHistoryCollection = "pokemon_history";

    public async Task<List<TradeUserDTO>> ListUsers(string serverUrl, string token, CancellationToken ct)
    {
        using var doc = await SendJson(HttpMethod.Get,
            $"{serverUrl}/api/collections/{UsersCollection}/records?perPage=200&sort=name&fields=id,name", null, token, ct);

        return [.. doc.RootElement.GetProperty("items").EnumerateArray().Select(item => new TradeUserDTO(
            Id: Str(item, "id") ?? "",
            Name: Str(item, "name") ?? ""
        ))];
    }

    public async Task<List<PocketBaseTradeRecord>> ListTrades(string serverUrl, string token, CancellationToken ct)
    {
        using var doc = await SendJson(HttpMethod.Get,
            $"{serverUrl}/api/collections/{TradesCollection}/records?perPage=200&sort=-created", null, token, ct);

        return [.. doc.RootElement.GetProperty("items").EnumerateArray().Select(ReadTrade)];
    }

    public async Task<List<PocketBaseTradePokemonRecord>> ListPokemonToClaim(string serverUrl, string token, string userId, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"owner = '{EscapeFilter(userId)}' && status = 'TO_CLAIM'");
        using var doc = await SendJson(HttpMethod.Get,
            $"{serverUrl}/api/collections/{TradePokemonCollection}/records?perPage=100&sort=created&filter={filter}", null, token, ct);

        return [.. doc.RootElement.GetProperty("items").EnumerateArray().Select(item => new PocketBaseTradePokemonRecord(
            Id: Str(item, "id") ?? "",
            CollectionId: Str(item, "collectionId") ?? "",
            File: Str(item, "file") ?? "",
            Format: Str(item, "format") ?? "",
            Species: Int(item, "species"),
            Trade: Str(item, "trade")
        ))];
    }

    public async Task<List<PocketBaseHistoryRecord>> ListHistory(string serverUrl, string token, string fingerprint, CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($"fingerprint = '{EscapeFilter(fingerprint)}'");
        using var doc = await SendJson(HttpMethod.Get,
            $"{serverUrl}/api/collections/{PokemonHistoryCollection}/records?perPage=100&sort=created&filter={filter}", null, token, ct);

        return [.. doc.RootElement.GetProperty("items").EnumerateArray().Select(item => new PocketBaseHistoryRecord(
            Id: Str(item, "id") ?? "",
            FromUser: Str(item, "fromUser"),
            ToUser: Str(item, "toUser"),
            Trade: Str(item, "trade"),
            Created: ParseDate(Str(item, "created"))
        ))];
    }

    /// <summary>A pokémon in custody (visible to the owner and both sides of its trade).</summary>
    public async Task<PocketBaseTradePokemonRecord> GetTradePokemon(string serverUrl, string token, string pokemonId, CancellationToken ct)
    {
        using var doc = await SendJson(HttpMethod.Get,
            $"{serverUrl}/api/collections/{TradePokemonCollection}/records/{Uri.EscapeDataString(pokemonId)}", null, token, ct);
        var item = doc.RootElement;
        return new(
            Id: Str(item, "id") ?? "",
            CollectionId: Str(item, "collectionId") ?? "",
            File: Str(item, "file") ?? "",
            Format: Str(item, "format") ?? "",
            Species: Int(item, "species"),
            Trade: Str(item, "trade")
        );
    }

    public async Task<byte[]> DownloadTradePokemon(string serverUrl, string token, PocketBaseTradePokemonRecord record, CancellationToken ct)
    {
        string fileToken;
        using (var doc = await SendJson(HttpMethod.Post, $"{serverUrl}/api/files/token", null, token, ct))
            fileToken = doc.RootElement.GetProperty("token").GetString()!;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);

        var url = $"{serverUrl}/api/files/{Uri.EscapeDataString(record.CollectionId)}/{Uri.EscapeDataString(record.Id)}/{Uri.EscapeDataString(record.File)}"
            + $"?token={Uri.EscapeDataString(fileToken)}";
        using var response = await http.GetAsync(url, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
            throw new PocketBaseException(response.StatusCode, $"Server error {(int)response.StatusCode}: pokémon download failed");

        return await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
    }

    public async Task<PocketBaseTradeRecord> CreateTradeOffer(
        string serverUrl, string token, string toUserId, string? message, TradeKind kind, TradePokemonUpload pokemon, CancellationToken ct
    )
    {
        using var form = PokemonForm(pokemon);
        form.Add(new StringContent(toUserId), "to");
        form.Add(new StringContent(kind.ToString()), "kind");
        form.Add(new StringContent(message ?? ""), "message");

        using var doc = await SendJson(HttpMethod.Post, $"{serverUrl}/api/pokehome/trades", form, token, ct);
        return ReadTrade(doc.RootElement);
    }

    public async Task<PocketBaseTradeRecord> CounterTrade(
        string serverUrl, string token, string tradeId, string? message, TradePokemonUpload pokemon, CancellationToken ct
    )
    {
        using var form = PokemonForm(pokemon);
        form.Add(new StringContent(message ?? ""), "counterMessage");

        using var doc = await SendJson(HttpMethod.Post, $"{serverUrl}/api/pokehome/trades/{Uri.EscapeDataString(tradeId)}/counter", form, token, ct);
        return ReadTrade(doc.RootElement);
    }

    /// <param name="action">accept, decline or cancel</param>
    public async Task<PocketBaseTradeRecord> TradeAction(string serverUrl, string token, string tradeId, string action, CancellationToken ct)
    {
        using var doc = await SendJson(HttpMethod.Post,
            $"{serverUrl}/api/pokehome/trades/{Uri.EscapeDataString(tradeId)}/{Uri.EscapeDataString(action)}", null, token, ct);
        return ReadTrade(doc.RootElement);
    }

    public async Task MarkPokemonClaimed(string serverUrl, string token, string pokemonId, CancellationToken ct)
    {
        using var _ = await SendJson(HttpMethod.Post,
            $"{serverUrl}/api/pokehome/pokemon/{Uri.EscapeDataString(pokemonId)}/claimed", null, token, ct);
    }

    private static MultipartFormDataContent PokemonForm(TradePokemonUpload pokemon)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(pokemon.Fingerprint), "fingerprint" },
            { new StringContent(pokemon.Format), "format" },
            { new StringContent(pokemon.Species.ToString()), "species" },
            { new StringContent(pokemon.Form.ToString()), "form" },
            { new StringContent(pokemon.Nickname), "nickname" },
            { new StringContent(pokemon.Level.ToString()), "level" },
            { new StringContent(pokemon.Shiny ? "true" : "false"), "shiny" },
            { new StringContent(pokemon.Gender.ToString()), "gender" },
            { new StringContent(pokemon.OtName), "otName" },
            { new StringContent(pokemon.Tid.ToString()), "tid" },
            { new StringContent(pokemon.Version.ToString()), "version" },
        };
        var file = new ByteArrayContent(pokemon.Content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", $"pokemon.{pokemon.Format}");
        return form;
    }

    private static PocketBaseTradeRecord ReadTrade(JsonElement item) => new(
        Id: Str(item, "id") ?? "",
        From: Str(item, "from") ?? "",
        To: Str(item, "to") ?? "",
        Status: Enum.TryParse<TradeStatus>(Str(item, "status"), out var status) ? status : TradeStatus.OPEN,
        Kind: Str(item, "kind") == "GIFT" ? TradeKind.GIFT : TradeKind.TRADE,
        Message: Str(item, "message"),
        CounterMessage: Str(item, "counterMessage"),
        Offered: ReadSummary(item, "offeredSummary"),
        Countered: ReadSummary(item, "counteredSummary"),
        OfferedPokemonId: Str(item, "offered"),
        CounteredPokemonId: Str(item, "countered"),
        Created: ParseDate(Str(item, "created")),
        ExpiresAt: ParseDate(Str(item, "expiresAt")),
        ClosedAt: Str(item, "closedAt") is string closed ? ParseDate(closed) : null
    );

    private static TradePokemonSummaryDTO? ReadSummary(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var summary) || summary.ValueKind != JsonValueKind.Object)
            return null;

        return new(
            Species: Int(summary, "species"),
            Form: Int(summary, "form"),
            Nickname: Str(summary, "nickname") ?? "",
            Level: Int(summary, "level"),
            Shiny: summary.TryGetProperty("shiny", out var shiny) && shiny.ValueKind == JsonValueKind.True,
            Gender: Int(summary, "gender"),
            OtName: Str(summary, "otName") ?? "",
            Tid: Int(summary, "tid"),
            Version: Int(summary, "version"),
            Format: Str(summary, "format") ?? "",
            Fingerprint: Str(summary, "fingerprint") ?? ""
        );
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() != "" ? v.GetString() : null;

    private static int Int(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

    private static string EscapeFilter(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}

public record PocketBaseTradeRecord(
    string Id, string From, string To, TradeStatus Status, TradeKind Kind, string? Message, string? CounterMessage,
    TradePokemonSummaryDTO? Offered, TradePokemonSummaryDTO? Countered,
    DateTime Created, DateTime ExpiresAt, DateTime? ClosedAt,
    string? OfferedPokemonId = null, string? CounteredPokemonId = null
);

public record PocketBaseTradePokemonRecord(string Id, string CollectionId, string File, string Format, int Species, string? Trade);

public record PocketBaseHistoryRecord(string Id, string? FromUser, string? ToUser, string? Trade, DateTime Created);

/// <summary>A pokémon file sent to the server, with the fields shown in the trade list.</summary>
public record TradePokemonUpload(
    byte[] Content, string Format, string Fingerprint,
    int Species, int Form, string Nickname, int Level, bool Shiny, int Gender, string OtName, int Tid, int Version
);
