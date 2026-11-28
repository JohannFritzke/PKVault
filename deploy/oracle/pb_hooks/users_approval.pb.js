/// <reference path="../pb_data/types.d.ts" />

// User approval (see pb_migrations/1795500000_users_approval.js).

// a new account always starts PENDING, whatever it was created with (only a superuser may choose)
onRecordCreateRequest((e) => {
    // sign-up through Google / GitHub runs this hook with the "oauth2" request context
    const oauth2 = e.requestInfo().context === "oauth2";
    if (!oauth2 && !e.hasSuperuserAuth()) {
        throw new ForbiddenError("Accounts are created by logging in with Google or GitHub.");
    }
    if (!e.hasSuperuserAuth()) {
        e.record.set("status", "PENDING");
    }
    e.next();
}, "users");

// sign-up through Google / GitHub (covers the case where it does not go through the create request hook)
onRecordCreate((e) => {
    if (!e.record.getString("status")) {
        e.record.set("status", "PENDING");
    }
    e.next();
}, "users");
