/// <reference path="../pb_data/types.d.ts" />

// vault_backups: full PKVault backups (.zip made on "Save") uploaded by each user.
// Only the owner can list/view/create/delete. Records are never updated: a new backup is a new record.
// `parent` = the backup this one was based on; two records with the same parent mean a conflict.
migrate((app) => {
    const users = app.findCollectionByNameOrId("users");

    const collection = new Collection({
        type: "base",
        name: "vault_backups",
        listRule: "owner = @request.auth.id",
        viewRule: "owner = @request.auth.id",
        createRule: "@request.auth.id != '' && owner = @request.auth.id",
        updateRule: null,
        deleteRule: "owner = @request.auth.id",
        fields: [
            {
                type: "relation",
                name: "owner",
                required: true,
                collectionId: users.id,
                maxSelect: 1,
                cascadeDelete: true,
            },
            {
                type: "file",
                name: "file",
                required: true,
                maxSelect: 1,
                maxSize: 200 * 1024 * 1024,
                mimeTypes: [ "application/zip", "application/x-zip-compressed" ],
                protected: true,
            },
            {
                type: "text",
                name: "sha256",
                required: true,
                min: 64,
                max: 64,
                pattern: "^[a-f0-9]{64}$",
            },
            {
                type: "text",
                name: "device",
                required: true,
                max: 100,
            },
            {
                type: "text",
                name: "appVersion",
                max: 50,
            },
            {
                type: "autodate",
                name: "created",
                onCreate: true,
                onUpdate: false,
            },
        ],
        indexes: [
            "CREATE INDEX idx_vault_backups_owner_created ON vault_backups (owner, created)",
            "CREATE UNIQUE INDEX idx_vault_backups_owner_sha256 ON vault_backups (owner, sha256)",
        ],
    });
    app.save(collection);

    // self relation: needs the collection id, so it is added after the first save
    collection.fields.add(new RelationField({
        name: "parent",
        collectionId: collection.id,
        maxSelect: 1,
        cascadeDelete: false,
    }));
    app.save(collection);
}, (app) => {
    const collection = app.findCollectionByNameOrId("vault_backups");
    app.delete(collection);
});
