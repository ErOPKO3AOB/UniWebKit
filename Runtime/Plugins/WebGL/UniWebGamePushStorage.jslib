mergeInto(LibraryManager.library, {
    $UniWebGamePushStorage__deps: ["$UniWebStorage"],
    $UniWebGamePushStorage: {
        gp: function () {
            var sdk = typeof _GP === "function" ? _GP() : (typeof window !== "undefined" ? window.GamePush : null);
            return sdk && sdk.gp ? sdk.gp : sdk;
        },
        field: function (gp, key) {
            var fields = gp.player.fields || [];
            var field = Array.isArray(fields) ? fields.find(function (item) { return item.key === key; }) : null;
            if (!field && (key === "score" || key === "name" || key === "avatar"))
                field = { key: key, type: key === "score" ? "number" : "string" };
            return field;
        },
        request: async function (operation, key, profile, document, bindingsJson) {
            var self = UniWebGamePushStorage;
            var gp = self.gp();
            if (!gp || !gp.player) throw UniWebStorage.error(2, "GamePush is not ready.");
            await gp.player.ready;
            var checkPlayer = function () {
                if (String(gp.player.id) !== profile) throw UniWebStorage.error(9, "The GamePush player changed.");
            };
            checkPlayer();
            var rootField = self.field(gp, key);
            if (!rootField || rootField.type !== "string")
                throw UniWebStorage.error(8, "Create the GamePush player string field '" + key + "' with an empty string default.");
            if (operation === "read") {
                var stored = gp.player.get(key);
                return stored == null ? "" : typeof stored === "string" ? stored : JSON.stringify(stored);
            }
            var save = JSON.parse(document);
            if (save.owner !== profile) throw UniWebStorage.error(9, "The save belongs to another player.");
            var bindings = bindingsJson ? JSON.parse(bindingsJson).bindings || [] : [];
            var prepared = bindings.filter(function (binding) { return binding && binding.Key && binding.PlatformField; })
                .map(function (binding) {
                    if (binding.PlatformField === key) throw UniWebStorage.error(8, "A mirrored field cannot replace the save document field.");
                    var entry = save.entries.find(function (item) { return item.key === binding.Key; });
                    if (!entry) return null; // Deleting a portable key does not reset unrelated native fields.
                    if (entry.value.type !== binding.Type) throw UniWebStorage.error(7, "Type mismatch for '" + binding.Key + "'.");
                    var nativeField = self.field(gp, binding.PlatformField);
                    var kind = binding.Type === 1 || binding.Type === 3 ? "number" : binding.Type === 6 ? "boolean" : "string";
                    if (!nativeField || nativeField.type !== kind)
                        throw UniWebStorage.error(8, "Check the GamePush field and type for '" + binding.PlatformField + "'.");
                    var data = entry.value.data;
                    var value = kind === "number" ? Number(data) : kind === "boolean" ? data.toLowerCase() === "true" : data;
                    if (kind === "number" && !Number.isFinite(value)) throw UniWebStorage.error(7, "Invalid numeric value.");
                    return { field: binding.PlatformField, value: value };
                }).filter(function (item) { return item !== null; });
            gp.player.set(key, document);
            prepared.forEach(function (item) { gp.player.set(item.field, item.value); });
            var syncResult = await gp.player.sync({ storage: "cloud" });
            if (syncResult === false || syncResult && syncResult.success === false)
                throw UniWebStorage.error(12, "GamePush did not complete cloud synchronization.");
            checkPlayer();
            var current = gp.player.get(key);
            var matches = typeof current === "string" ? current === document : JSON.stringify(current) === JSON.stringify(save);
            if (!matches || prepared.some(function (item) { return gp.player.get(item.field) !== item.value; }))
                throw UniWebStorage.error(10, "The synchronized data differs from the submitted save.");
            return "";
        }
    },
    UniWebStorage_Request__deps: ["$UniWebStorage", "$UniWebGamePushStorage", "$UTF8ToString"],
    UniWebStorage_Request: function (host, id, backend, operation, key, profile, document, bindings) {
        var operationName = UTF8ToString(operation), keyName = UTF8ToString(key);
        var profileId = UTF8ToString(profile), value = UTF8ToString(document), fields = UTF8ToString(bindings);
        UniWebStorage.run(UTF8ToString(host), UTF8ToString(id), keyName, profileId, operationName === "write",
            function () { return UniWebGamePushStorage.request(operationName, keyName, profileId, value, fields); });
    }
});
