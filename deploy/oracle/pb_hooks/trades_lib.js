// Shared code of pb_hooks/trades.pb.js (loaded with require: PocketBase runs each handler in isolation).

const EXPIRES_DAYS = 3;
const MAX_OPEN_TRADES_PER_USER = 20;

const POKEMON_TEXT_FIELDS = [ "fingerprint", "format", "nickname", "otName" ];
const POKEMON_NUMBER_FIELDS = [ "species", "form", "level", "gender", "tid", "version" ];

/** PocketBase date string, `days` from now. */
function dateFromNow(days) {
    return new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString().replace("T", " ");
}

function now() {
    return new Date().toISOString().replace("T", " ");
}

function readBodyValue(body, name) {
    const value = body[ name ];
    if (Array.isArray(value)) {
        return value.length > 0 ? value[ 0 ] : undefined;
    }
    return value;
}

/**
 * Creates a trade_pokemon in custody from a multipart request (file + display fields).
 */
function createCustodyPokemon(e, txApp, ownerId, tradeId) {
    const body = e.requestInfo().body;
    const files = e.findUploadedFiles("file");
    if (!files || files.length !== 1) {
        throw new BadRequestError("A single pokémon file is required.");
    }

    const record = new Record(txApp.findCollectionByNameOrId("trade_pokemon"));
    record.set("owner", ownerId);
    record.set("status", "CUSTODY");
    record.set("file", files[ 0 ]);

    for (const name of POKEMON_TEXT_FIELDS) {
        const value = readBodyValue(body, name);
        record.set(name, value === undefined || value === null ? "" : String(value));
    }
    for (const name of POKEMON_NUMBER_FIELDS) {
        const value = parseInt(String(readBodyValue(body, name) ?? "0"), 10);
        record.set(name, isNaN(value) ? 0 : value);
    }
    record.set("shiny", String(readBodyValue(body, "shiny")) === "true");

    if (tradeId) {
        record.set("trade", tradeId);
    }

    txApp.save(record);
    return record;
}

function summary(pokemon) {
    const result = { id: pokemon.id, shiny: pokemon.getBool("shiny") };
    for (const name of POKEMON_TEXT_FIELDS) {
        result[ name ] = pokemon.getString(name);
    }
    for (const name of POKEMON_NUMBER_FIELDS) {
        result[ name ] = pokemon.getInt(name);
    }
    return result;
}

function findPokemon(txApp, id) {
    if (!id) {
        return null;
    }
    try {
        return txApp.findRecordById("trade_pokemon", id);
    } catch (_) {
        return null;
    }
}

/** Gives a pokémon to `ownerId`, ready to be claimed by that user's app. */
function giveTo(txApp, pokemon, ownerId) {
    pokemon.set("owner", ownerId);
    pokemon.set("status", "TO_CLAIM");
    txApp.save(pokemon);
}

function addHistory(txApp, pokemon, fromId, toId, tradeId) {
    const record = new Record(txApp.findCollectionByNameOrId("pokemon_history"));
    record.set("fingerprint", pokemon.getString("fingerprint"));
    record.set("species", pokemon.getInt("species"));
    record.set("nickname", pokemon.getString("nickname"));
    record.set("fromUser", fromId);
    record.set("toUser", toId);
    record.set("trade", tradeId);
    txApp.save(record);
}

/** Ends a trade without exchange: every pokémon goes back to the user who put it in. */
function closeWithReturn(txApp, trade, status, closedBy) {
    const offered = findPokemon(txApp, trade.getString("offered"));
    if (offered && offered.getString("status") === "CUSTODY") {
        giveTo(txApp, offered, trade.getString("from"));
    }
    const countered = findPokemon(txApp, trade.getString("countered"));
    if (countered && countered.getString("status") === "CUSTODY") {
        giveTo(txApp, countered, trade.getString("to"));
    }

    trade.set("status", status);
    trade.set("closedAt", now());
    if (closedBy) {
        trade.set("closedBy", closedBy);
    }
    txApp.save(trade);
}

function findTradeForUpdate(txApp, e) {
    const id = e.request.pathValue("id");
    try {
        return txApp.findRecordById("trades", id);
    } catch (_) {
        throw new NotFoundError("Trade not found.");
    }
}

// trades are only for accounts approved by the server owner
function requireApproved(e) {
    if (!e.auth || e.auth.getString("status") !== "APPROVED") {
        throw new ForbiddenError("Your account is waiting for approval.");
    }
}

module.exports = {
    requireApproved,
    EXPIRES_DAYS,
    MAX_OPEN_TRADES_PER_USER,
    dateFromNow,
    now,
    readBodyValue,
    createCustodyPokemon,
    summary,
    findPokemon,
    giveTo,
    addHistory,
    closeWithReturn,
    findTradeForUpdate,
};
