/// <reference path="../pb_data/types.d.ts" />

// User approval (security):
// - users get a status PENDING | APPROVED | BLOCKED; new accounts start PENDING (pb_hooks/users_approval.pb.js),
//   the server owner approves them in the admin panel. Accounts that exist now become PENDING too.
// - only APPROVED users can use backups, trades and see other (approved) users.
// - users log in only with Google / GitHub: email + password login is turned off for them
//   (superusers of the admin panel are a separate collection and keep their password).
// - users cannot change their own record (status included); only superusers can.
// - the built-in rate limiter is turned on (login attempts included).

const APPROVED = "@request.auth.status = 'APPROVED'";

const RULES = {
    vault_backups: {
        listRule: `${APPROVED} && owner = @request.auth.id`,
        viewRule: `${APPROVED} && owner = @request.auth.id`,
        createRule: `${APPROVED} && owner = @request.auth.id`,
        deleteRule: `${APPROVED} && owner = @request.auth.id`,
    },
    trades: {
        listRule: `${APPROVED} && (from = @request.auth.id || to = @request.auth.id)`,
        viewRule: `${APPROVED} && (from = @request.auth.id || to = @request.auth.id)`,
    },
    trade_pokemon: {
        listRule: `${APPROVED} && (owner = @request.auth.id || trade.from = @request.auth.id || trade.to = @request.auth.id)`,
        viewRule: `${APPROVED} && (owner = @request.auth.id || trade.from = @request.auth.id || trade.to = @request.auth.id)`,
    },
    pokemon_history: {
        listRule: APPROVED,
        viewRule: APPROVED,
    },
};

// rules before this migration (for the down migration)
const PREVIOUS = {
    vault_backups: {
        listRule: "owner = @request.auth.id",
        viewRule: "owner = @request.auth.id",
        createRule: "@request.auth.id != '' && owner = @request.auth.id",
        deleteRule: "owner = @request.auth.id",
    },
    trades: {
        listRule: "from = @request.auth.id || to = @request.auth.id",
        viewRule: "from = @request.auth.id || to = @request.auth.id",
    },
    trade_pokemon: {
        listRule: "owner = @request.auth.id || trade.from = @request.auth.id || trade.to = @request.auth.id",
        viewRule: "owner = @request.auth.id || trade.from = @request.auth.id || trade.to = @request.auth.id",
    },
    pokemon_history: {
        listRule: "@request.auth.id != ''",
        viewRule: "@request.auth.id != ''",
    },
};

function applyRules(app, rules) {
    for (const [ name, values ] of Object.entries(rules)) {
        const collection = app.findCollectionByNameOrId(name);
        for (const [ key, value ] of Object.entries(values)) {
            collection[ key ] = value;
        }
        app.save(collection);
    }
}

migrate((app) => {
    const users = app.findCollectionByNameOrId("users");
    users.fields.add(new SelectField({ name: "status", maxSelect: 1, values: [ "PENDING", "APPROVED", "BLOCKED" ] }));
    users.passwordAuth.enabled = false;
    // own record always (the app reads its status); others only between approved users
    users.listRule = `id = @request.auth.id || (${APPROVED} && status = 'APPROVED')`;
    users.viewRule = users.listRule;
    users.updateRule = null;
    users.deleteRule = null;
    // createRule stays "" : Google / GitHub sign-up needs it. Plain API creation is refused by the hook.
    app.save(users);

    app.db().newQuery("UPDATE users SET status = 'PENDING'").execute();

    applyRules(app, RULES);

    const settings = app.settings();
    settings.rateLimits.enabled = true;
    app.save(settings);
}, (app) => {
    const users = app.findCollectionByNameOrId("users");
    users.fields.removeByName("status");
    users.passwordAuth.enabled = true;
    users.listRule = "@request.auth.id != ''";
    users.viewRule = "@request.auth.id != ''";
    users.updateRule = "id = @request.auth.id";
    users.deleteRule = "id = @request.auth.id";
    app.save(users);

    applyRules(app, PREVIOUS);
});
