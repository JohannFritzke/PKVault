/// <reference path="../pb_data/types.d.ts" />

// Login session of the app: 30 days instead of the PocketBase default (5 days).
// The app refreshes its token when it starts and once a day while open, so only a computer
// that stays closed for more than 30 days has to log in again.
// To log everyone out at once: regenerate the users token secret (see deploy/oracle/README.md).

const DAYS_30 = 30 * 24 * 60 * 60;
const DEFAULT_5_DAYS = 5 * 24 * 60 * 60;

migrate((app) => {
    const users = app.findCollectionByNameOrId("users");
    users.authToken.duration = DAYS_30;
    app.save(users);
}, (app) => {
    const users = app.findCollectionByNameOrId("users");
    users.authToken.duration = DEFAULT_5_DAYS;
    app.save(users);
});
