/// <reference path="../pb_data/types.d.ts" />

// Who closed a trade (declined or cancelled it), so each side can read
// "declined by you" / "declined by your friend". Set only by the trade routes.
migrate((app) => {
    const trades = app.findCollectionByNameOrId("trades");
    trades.fields.add(new TextField({ name: "closedBy", max: 15 }));
    app.save(trades);
}, (app) => {
    const trades = app.findCollectionByNameOrId("trades");
    trades.fields.removeByName("closedBy");
    app.save(trades);
});
