/// <reference path="../pb_data/types.d.ts" />

// Trade routes (phase 4). Every state change happens here, in one transaction.
//
//   POST /api/pokehome/trades                  offer a pokémon to a friend (multipart: to, kind TRADE|GIFT, message, file + display fields)
//   POST /api/pokehome/trades/{id}/counter     receiver answers a TRADE with a pokémon (multipart: counterMessage, file + display fields)
//   POST /api/pokehome/trades/{id}/accept      receiver on an OPEN GIFT = take it; sender on COUNTERED = exchange
//   POST /api/pokehome/trades/{id}/decline     receiver (OPEN/COUNTERED) or sender (COUNTERED): pokémon go back
//   POST /api/pokehome/trades/{id}/cancel      sender (OPEN/COUNTERED): pokémon go back
//   POST /api/pokehome/pokemon/{id}/claimed    the owner's app stored the pokémon locally: the server copy is deleted
//
// Open trades expire after 3 days (cron below).

routerAdd("POST", "/api/pokehome/trades", (e) => {
    require(`${__hooks}/trades_lib.js`).requireApproved(e);
    const lib = require(`${__hooks}/trades_lib.js`);
    const me = e.auth.id;
    const body = e.requestInfo().body;
    const to = String(lib.readBodyValue(body, "to") || "");
    const kind = String(lib.readBodyValue(body, "kind") || "TRADE");
    if (kind !== "TRADE" && kind !== "GIFT") {
        throw new BadRequestError("Unknown trade kind.");
    }

    if (!to || to === me) {
        throw new BadRequestError("Choose a friend to trade with.");
    }
    let toUser = null;
    try {
        toUser = e.app.findRecordById("users", to);
    } catch (_) { }
    if (!toUser || toUser.getString("status") !== "APPROVED") {
        throw new BadRequestError("This user does not exist.");
    }

    const openCount = e.app.countRecords("trades",
        $dbx.exp("`from` = {:me} AND status IN ('OPEN', 'COUNTERED')", { me: me }));
    if (openCount >= lib.MAX_OPEN_TRADES_PER_USER) {
        throw new BadRequestError("Too many open trades.");
    }

    let trade;
    e.app.runInTransaction((txApp) => {
        const pokemon = lib.createCustodyPokemon(e, txApp, me, null);

        trade = new Record(txApp.findCollectionByNameOrId("trades"));
        trade.set("from", me);
        trade.set("to", to);
        trade.set("status", "OPEN");
        trade.set("kind", kind);
        trade.set("message", String(lib.readBodyValue(body, "message") || "").slice(0, 300));
        trade.set("offered", pokemon.id);
        trade.set("offeredSummary", lib.summary(pokemon));
        trade.set("expiresAt", lib.dateFromNow(lib.EXPIRES_DAYS));
        txApp.save(trade);

        pokemon.set("trade", trade.id);
        txApp.save(pokemon);
    });

    return e.json(200, trade);
}, $apis.requireAuth("users"));

routerAdd("POST", "/api/pokehome/trades/{id}/counter", (e) => {
    require(`${__hooks}/trades_lib.js`).requireApproved(e);
    const lib = require(`${__hooks}/trades_lib.js`);
    const me = e.auth.id;
    const body = e.requestInfo().body;

    let trade;
    e.app.runInTransaction((txApp) => {
        trade = lib.findTradeForUpdate(txApp, e);
        if (trade.getString("to") !== me) {
            throw new ForbiddenError("Only the receiver can answer this trade.");
        }
        if (trade.getString("status") !== "OPEN") {
            throw new BadRequestError("This trade is not open anymore.");
        }
        if (trade.getString("kind") === "GIFT") {
            throw new BadRequestError("A gift cannot be answered with a pokémon.");
        }

        const pokemon = lib.createCustodyPokemon(e, txApp, me, trade.id);

        trade.set("status", "COUNTERED");
        trade.set("countered", pokemon.id);
        trade.set("counteredSummary", lib.summary(pokemon));
        trade.set("counterMessage", String(lib.readBodyValue(body, "counterMessage") || "").slice(0, 300));
        // the sender gets time to answer
        trade.set("expiresAt", lib.dateFromNow(lib.EXPIRES_DAYS));
        txApp.save(trade);
    });

    return e.json(200, trade);
}, $apis.requireAuth("users"));

routerAdd("POST", "/api/pokehome/trades/{id}/accept", (e) => {
    require(`${__hooks}/trades_lib.js`).requireApproved(e);
    const lib = require(`${__hooks}/trades_lib.js`);
    const me = e.auth.id;

    let trade;
    e.app.runInTransaction((txApp) => {
        trade = lib.findTradeForUpdate(txApp, e);
        const status = trade.getString("status");
        const from = trade.getString("from");
        const to = trade.getString("to");

        const offered = lib.findPokemon(txApp, trade.getString("offered"));
        if (!offered || offered.getString("status") !== "CUSTODY") {
            throw new BadRequestError("The offered pokémon is not in custody anymore.");
        }

        if (status === "OPEN" && me === to && trade.getString("kind") === "GIFT") {
            lib.giveTo(txApp, offered, to);
            lib.addHistory(txApp, offered, from, to, trade.id);
        } else if (status === "COUNTERED" && me === from) {
            const countered = lib.findPokemon(txApp, trade.getString("countered"));
            if (!countered || countered.getString("status") !== "CUSTODY") {
                throw new BadRequestError("The counter-offered pokémon is not in custody anymore.");
            }
            lib.giveTo(txApp, offered, to);
            lib.giveTo(txApp, countered, from);
            lib.addHistory(txApp, offered, from, to, trade.id);
            lib.addHistory(txApp, countered, to, from, trade.id);
        } else {
            throw new BadRequestError("This trade cannot be accepted by you now.");
        }

        trade.set("status", "DONE");
        trade.set("closedAt", lib.now());
        txApp.save(trade);
    });

    return e.json(200, trade);
}, $apis.requireAuth("users"));

routerAdd("POST", "/api/pokehome/trades/{id}/decline", (e) => {
    require(`${__hooks}/trades_lib.js`).requireApproved(e);
    const lib = require(`${__hooks}/trades_lib.js`);
    const me = e.auth.id;

    let trade;
    e.app.runInTransaction((txApp) => {
        trade = lib.findTradeForUpdate(txApp, e);
        const status = trade.getString("status");
        const allowed = (me === trade.getString("to") && (status === "OPEN" || status === "COUNTERED"))
            || (me === trade.getString("from") && status === "COUNTERED");
        if (!allowed) {
            throw new BadRequestError("This trade cannot be declined by you now.");
        }
        lib.closeWithReturn(txApp, trade, "DECLINED", me);
    });

    return e.json(200, trade);
}, $apis.requireAuth("users"));

routerAdd("POST", "/api/pokehome/trades/{id}/cancel", (e) => {
    require(`${__hooks}/trades_lib.js`).requireApproved(e);
    const lib = require(`${__hooks}/trades_lib.js`);
    const me = e.auth.id;

    let trade;
    e.app.runInTransaction((txApp) => {
        trade = lib.findTradeForUpdate(txApp, e);
        const status = trade.getString("status");
        if (me !== trade.getString("from") || (status !== "OPEN" && status !== "COUNTERED")) {
            throw new BadRequestError("This trade cannot be cancelled by you now.");
        }
        lib.closeWithReturn(txApp, trade, "CANCELLED", me);
    });

    return e.json(200, trade);
}, $apis.requireAuth("users"));

routerAdd("POST", "/api/pokehome/pokemon/{id}/claimed", (e) => {
    require(`${__hooks}/trades_lib.js`).requireApproved(e);
    const me = e.auth.id;
    const id = e.request.pathValue("id");

    e.app.runInTransaction((txApp) => {
        let pokemon;
        try {
            pokemon = txApp.findRecordById("trade_pokemon", id);
        } catch (_) {
            // already claimed: nothing to do
            return;
        }
        if (pokemon.getString("owner") !== me || pokemon.getString("status") !== "TO_CLAIM") {
            throw new BadRequestError("This pokémon cannot be claimed by you.");
        }
        txApp.delete(pokemon);
    });

    return e.json(200, { id: id });
}, $apis.requireAuth("users"));

// every 15 minutes: open trades older than 3 days go back to their owners
cronAdd("pokehome_trades_expire", "*/15 * * * *", () => {
    const lib = require(`${__hooks}/trades_lib.js`);

    const expired = $app.findRecordsByFilter(
        "trades",
        "(status = 'OPEN' || status = 'COUNTERED') && expiresAt < {:now}",
        "expiresAt",
        200,
        0,
        { now: lib.now() },
    );

    for (const trade of expired) {
        try {
            $app.runInTransaction((txApp) => {
                const fresh = txApp.findRecordById("trades", trade.id);
                const status = fresh.getString("status");
                if (status === "OPEN" || status === "COUNTERED") {
                    lib.closeWithReturn(txApp, fresh, "EXPIRED");
                }
            });
        } catch (err) {
            $app.logger().error("trade expiry failed", "trade", trade.id, "error", String(err));
        }
    }

    if (expired.length > 0) {
        $app.logger().info("trades expired", "count", expired.length);
    }
});
