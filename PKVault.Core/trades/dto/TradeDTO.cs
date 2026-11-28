namespace PKVault.Core.Trades;

public enum TradeStatus
{
    /// <summary>Waiting for the friend: accept as a gift, counter-offer or decline.</summary>
    OPEN,
    /// <summary>The friend offered a pokémon back: the sender accepts or declines.</summary>
    COUNTERED,
    DONE,
    DECLINED,
    CANCELLED,
    /// <summary>Not answered within 3 days: pokémon went back to their owners.</summary>
    EXPIRED,
}

public enum TradeKind
{
    /// <summary>The friend must answer with a pokémon.</summary>
    TRADE,
    /// <summary>The friend accepts or declines, nothing is given back.</summary>
    GIFT,
}

public enum TradeError
{
    NOT_LOGGED_IN,
    /// <summary>The server owner has not approved this account (or blocked it).</summary>
    NOT_APPROVED,
    /// <summary>Unsaved actions: save or discard them first (the pokémon leaves the storage with a save).</summary>
    PENDING_CHANGES,
    /// <summary>Pokémon attached to a save, external, egg or invalid.</summary>
    CANNOT_TRADE,
    /// <summary>Another copy of this pokémon exists in a save or elsewhere in the storage (after "detach" for example).</summary>
    DUPLICATE,
    NOT_FOUND,
    SERVER,
}

public record TradeUserDTO(string Id, string Name);

public record TradePokemonSummaryDTO(
    int Species, int Form, string Nickname, int Level, bool Shiny, int Gender,
    string OtName, int Tid, int Version, string Format, string Fingerprint
);

public record TradeDTO(
    string Id,
    TradeStatus Status,
    TradeKind Kind,
    string FromUserId,
    string FromName,
    string ToUserId,
    string ToName,
    /// <summary>True when the current user made the offer.</summary>
    bool IsSender,
    string? Message,
    string? CounterMessage,
    TradePokemonSummaryDTO? Offered,
    TradePokemonSummaryDTO? Countered,
    DateTime Created,
    DateTime ExpiresAt,
    DateTime? ClosedAt
);

public record TradesStatusDTO(
    bool LoggedIn,
    string? UserId,
    List<TradeDTO> Trades,
    /// <summary>Other users, to choose who receives an offer.</summary>
    List<TradeUserDTO> Friends,
    /// <summary>Pokémon waiting on the server to be stored here.</summary>
    int ToClaimCount,
    /// <summary>Increases every time received pokémon are stored: the app reloads its storage.</summary>
    int ClaimRevision,
    TradeError? Error,
    string? ErrorDetail
);

public record TradeOfferRequestDTO(string PkmVariantId, string ToUserId, string? Message, bool Gift = false);

public record TradeCounterRequestDTO(string PkmVariantId, string? Message);

public record TradeActionResultDTO(bool Success, TradeError? Error, string? ErrorDetail);

public record PokemonHistoryEntryDTO(string FromName, string ToName, DateTime Date);

public class TradeException(TradeError error, string message) : Exception(message)
{
    public TradeError Error { get; } = error;
}
