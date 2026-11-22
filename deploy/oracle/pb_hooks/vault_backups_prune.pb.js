/// <reference path="../pb_data/types.d.ts" />

// Keeps only the newest backups of each user: after a new vault_backups record,
// older ones beyond KEEP are deleted (files included).
// A deleted record used as `parent` by a newer one only clears that optional relation.
onRecordAfterCreateSuccess((e) => {
    e.next();

    const KEEP = 30;
    const owner = e.record.get("owner");

    try {
        const old = e.app.findRecordsByFilter(
            "vault_backups",
            "owner = {:owner}",
            "-created",
            1000,   // limit
            KEEP,   // offset: skip the newest KEEP
            { owner: owner },
        );

        for (const record of old) {
            e.app.delete(record);
        }

        if (old.length > 0) {
            e.app.logger().info("vault_backups pruned", "owner", owner, "deleted", old.length);
        }
    } catch (err) {
        e.app.logger().error("vault_backups prune failed", "owner", owner, "error", String(err));
    }
}, "vault_backups");
