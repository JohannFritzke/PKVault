/// <reference path="../pb_data/types.d.ts" />

// Trades with custody (phase 4).
// - trade_pokemon: a pokémon file held by the server while it is in a trade, or waiting to be claimed.
// - trades: one offer from a user to a friend, optionally answered by a counter-offer.
// - pokemon_history: owner changes, keyed by the PKVault fingerprint (ImmutablePKM.GetPKMIdBase).
// Clients only read these collections: every change goes through the routes in pb_hooks/trades.pb.js.
// Users can see the other users (names only, emails stay hidden) to choose a friend.
migrate((app) => {
    const users = app.findCollectionByNameOrId("users");

    const pokemon = new Collection({
        type: "base",
        name: "trade_pokemon",
        createRule: null,
        updateRule: null,
        deleteRule: null,
        fields: [
            { type: "relation", name: "owner", required: true, collectionId: users.id, maxSelect: 1, cascadeDelete: true },
            // CUSTODY: locked in an open trade. TO_CLAIM: the owner's app must download it into its storage.
            { type: "select", name: "status", required: true, maxSelect: 1, values: [ "CUSTODY", "TO_CLAIM" ] },
            { type: "file", name: "file", required: true, maxSelect: 1, maxSize: 1024 * 1024, protected: true },
            { type: "text", name: "fingerprint", required: true, max: 200 },
            { type: "text", name: "format", required: true, max: 10 },
            { type: "number", name: "species", required: true, onlyInt: true, min: 1 },
            { type: "number", name: "form", onlyInt: true, min: 0 },
            { type: "text", name: "nickname", max: 50 },
            { type: "number", name: "level", onlyInt: true, min: 0, max: 100 },
            { type: "bool", name: "shiny" },
            { type: "number", name: "gender", onlyInt: true, min: 0, max: 2 },
            { type: "text", name: "otName", max: 50 },
            { type: "number", name: "tid", onlyInt: true, min: 0 },
            { type: "number", name: "version", onlyInt: true, min: 0 },
            { type: "autodate", name: "created", onCreate: true, onUpdate: false },
            { type: "autodate", name: "updated", onCreate: true, onUpdate: true },
        ],
        indexes: [
            "CREATE INDEX idx_trade_pokemon_owner_status ON trade_pokemon (owner, status)",
        ],
    });
    app.save(pokemon);

    const trades = new Collection({
        type: "base",
        name: "trades",
        listRule: "from = @request.auth.id || to = @request.auth.id",
        viewRule: "from = @request.auth.id || to = @request.auth.id",
        createRule: null,
        updateRule: null,
        deleteRule: null,
        fields: [
            { type: "relation", name: "from", required: true, collectionId: users.id, maxSelect: 1, cascadeDelete: true },
            { type: "relation", name: "to", required: true, collectionId: users.id, maxSelect: 1, cascadeDelete: true },
            { type: "select", name: "status", required: true, maxSelect: 1,
                values: [ "OPEN", "COUNTERED", "DONE", "DECLINED", "CANCELLED", "EXPIRED" ] },
            { type: "text", name: "message", max: 300 },
            { type: "text", name: "counterMessage", max: 300 },
            // not "required": the pokémon record is deleted once claimed (the summary stays)
            { type: "relation", name: "offered", collectionId: pokemon.id, maxSelect: 1, cascadeDelete: false },
            { type: "relation", name: "countered", collectionId: pokemon.id, maxSelect: 1, cascadeDelete: false },
            // copies for the history tab: trade_pokemon records are deleted once claimed
            { type: "json", name: "offeredSummary", maxSize: 4000 },
            { type: "json", name: "counteredSummary", maxSize: 4000 },
            { type: "date", name: "expiresAt", required: true },
            { type: "date", name: "closedAt" },
            { type: "autodate", name: "created", onCreate: true, onUpdate: false },
            { type: "autodate", name: "updated", onCreate: true, onUpdate: true },
        ],
        indexes: [
            "CREATE INDEX idx_trades_from ON trades (`from`, status)",
            "CREATE INDEX idx_trades_to ON trades (`to`, status)",
            "CREATE INDEX idx_trades_status_expires ON trades (status, expiresAt)",
        ],
    });
    app.save(trades);

    // back relation, needed by the trade_pokemon read rules
    pokemon.fields.add(new RelationField({ name: "trade", collectionId: trades.id, maxSelect: 1, cascadeDelete: false }));
    pokemon.listRule = "owner = @request.auth.id || trade.from = @request.auth.id || trade.to = @request.auth.id";
    pokemon.viewRule = pokemon.listRule;
    app.save(pokemon);

    const history = new Collection({
        type: "base",
        name: "pokemon_history",
        listRule: "@request.auth.id != ''",
        viewRule: "@request.auth.id != ''",
        createRule: null,
        updateRule: null,
        deleteRule: null,
        fields: [
            { type: "text", name: "fingerprint", required: true, max: 200 },
            { type: "number", name: "species", onlyInt: true },
            { type: "text", name: "nickname", max: 50 },
            { type: "relation", name: "fromUser", collectionId: users.id, maxSelect: 1, cascadeDelete: false },
            { type: "relation", name: "toUser", collectionId: users.id, maxSelect: 1, cascadeDelete: false },
            { type: "relation", name: "trade", collectionId: trades.id, maxSelect: 1, cascadeDelete: false },
            { type: "autodate", name: "created", onCreate: true, onUpdate: false },
        ],
        indexes: [
            "CREATE INDEX idx_pokemon_history_fingerprint ON pokemon_history (fingerprint, created)",
        ],
    });
    app.save(history);

    // friends can find each other (emails stay hidden by emailVisibility)
    users.listRule = "@request.auth.id != ''";
    users.viewRule = "@request.auth.id != ''";
    app.save(users);
}, (app) => {
    for (const name of [ "pokemon_history", "trades", "trade_pokemon" ]) {
        try {
            app.delete(app.findCollectionByNameOrId(name));
        } catch (_) { }
    }
    const users = app.findCollectionByNameOrId("users");
    users.listRule = "id = @request.auth.id";
    users.viewRule = "id = @request.auth.id";
    app.save(users);
});
