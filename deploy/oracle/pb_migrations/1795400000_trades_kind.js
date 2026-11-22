/// <reference path="../pb_data/types.d.ts" />

// Trade kind (phase 4, after the first real test):
// - TRADE: the friend must answer with a pokémon (counter-offer); it cannot be accepted as a gift.
// - GIFT: the friend accepts or declines, nothing is given back.
// Trades created before this migration become TRADE.
migrate((app) => {
    const trades = app.findCollectionByNameOrId("trades");
    trades.fields.add(new SelectField({ name: "kind", maxSelect: 1, values: [ "TRADE", "GIFT" ] }));
    app.save(trades);

    app.db().newQuery("UPDATE trades SET kind = 'TRADE' WHERE kind = '' OR kind IS NULL").execute();
}, (app) => {
    const trades = app.findCollectionByNameOrId("trades");
    trades.fields.removeByName("kind");
    app.save(trades);
});
