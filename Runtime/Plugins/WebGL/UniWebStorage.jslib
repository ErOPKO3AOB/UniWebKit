mergeInto(LibraryManager.library, {
    $UniWebStorage: {
        lanes: {},
        error: function (code, message) { return { storageCode: code, message: message }; },
        gp: function () {
            var sdk = typeof _GP === "function" ? _GP() : (typeof window !== "undefined" ? window.GamePush : null);
            return sdk && sdk.gp ? sdk.gp : sdk;
        },
        vk: function () {
            var bridge = typeof window !== "undefined" ? window.vkBridge : null;
            return bridge && bridge.default ? bridge.default : bridge;
        },
        vkProfile: function () {
            try { return new URLSearchParams(window.location.search).get("vk_user_id") || "guest"; }
            catch (_) { return "guest"; }
        },
        reply: function (host, id, result, error, cloudConfirmed) {
            var code = error && error.storageCode;
            if (error && !code) {
                var details = String(error.code || error.error_type || error.message || error);
                code = /rate.?limit|too.many.requests/i.test(details) ? 13 : /quota|too.large|too.long|limit/i.test(details) ? 11 : 12;
            }
            var value = {
                requestId: id, success: !error, cloudConfirmed: !error && cloudConfirmed,
                data: result || "", errorCode: code || 0,
                error: error ? String(error.message || error.error_reason && error.error_reason.error_msg || error) : ""
            };
            var text = JSON.stringify(value);
            if (typeof SendMessage === "function") SendMessage(host, "OnStorageReply", text);
            else if (typeof Module !== "undefined" && Module.SendMessage) Module.SendMessage(host, "OnStorageReply", text);
        },
        field: function (gp, key) {
            var fields = gp.player.fields || [];
            var field = Array.isArray(fields) ? fields.find(function (item) { return item.key === key; }) : null;
            if (!field && (key === "score" || key === "name" || key === "avatar"))
                field = { key: key, type: key === "score" ? "number" : "string" };
            return field;
        },
        run: function (host, id, backend, operation, key, profile, document, bindingsJson) {
            var self = UniWebStorage;
            var lane = backend + "|" + profile + "|" + key;
            var previous = self.lanes[lane] || Promise.resolve();
            // Keep SDK requests ordered even if C# cancels/times out while a write is in flight.
            var work = previous.catch(function () {}).then(async function () {
                if (!/^[a-zA-Z0-9_-]+$/.test(key))
                    throw self.error(8, "Cloud keys must contain only letters, digits, underscores and hyphens.");
                if (backend === "gamepush") {
                    var gp = self.gp();
                    if (!gp || !gp.player) throw self.error(2, "GamePush is not ready.");
                    await gp.player.ready;
                    var checkPlayer = function () {
                        if (String(gp.player.id) !== profile) throw self.error(9, "The GamePush player changed.");
                    };
                    checkPlayer();
                    var rootField = self.field(gp, key);
                    if (!rootField || rootField.type !== "string")
                        throw self.error(8, "Create the GamePush player string field '" + key + "' with an empty string default.");
                    if (operation === "read") {
                        var stored = gp.player.get(key);
                        return stored == null ? "" : typeof stored === "string" ? stored : JSON.stringify(stored);
                    }
                    var save = JSON.parse(document);
                    if (save.owner !== profile) throw self.error(9, "The save belongs to another player.");
                    var bindings = bindingsJson ? JSON.parse(bindingsJson).bindings || [] : [];
                    var prepared = bindings.filter(function (binding) { return binding && binding.Key && binding.PlatformField; })
                        .map(function (binding) {
                            if (binding.PlatformField === key) throw self.error(8, "A mirrored field cannot replace the save document field.");
                            var entry = save.entries.find(function (item) { return item.key === binding.Key; });
                            if (!entry) return null; // Deleting a portable key does not reset unrelated native fields.
                            if (entry.value.type !== binding.Type) throw self.error(7, "Type mismatch for '" + binding.Key + "'.");
                            var nativeField = self.field(gp, binding.PlatformField);
                            var kind = binding.Type === 1 || binding.Type === 3 ? "number" : binding.Type === 6 ? "boolean" : "string";
                            if (!nativeField || nativeField.type !== kind)
                                throw self.error(8, "Check the GamePush field and type for '" + binding.PlatformField + "'.");
                            var data = entry.value.data;
                            var value = kind === "number" ? Number(data) : kind === "boolean" ? data.toLowerCase() === "true" : data;
                            if (kind === "number" && !Number.isFinite(value)) throw self.error(7, "Invalid numeric value.");
                            return { field: binding.PlatformField, value: value };
                        }).filter(function (item) { return item !== null; });
                    gp.player.set(key, document);
                    prepared.forEach(function (item) { gp.player.set(item.field, item.value); });
                    var syncResult = await gp.player.sync({ storage: "cloud" });
                    if (syncResult === false || syncResult && syncResult.success === false)
                        throw self.error(12, "GamePush did not complete cloud synchronization.");
                    checkPlayer();
                    var current = gp.player.get(key);
                    var matches = typeof current === "string" ? current === document : JSON.stringify(current) === JSON.stringify(save);
                    if (!matches || prepared.some(function (item) { return gp.player.get(item.field) !== item.value; }))
                        throw self.error(10, "The synchronized data differs from the submitted save.");
                    return "";
                }
                if (backend === "vk") {
                    var bridge = self.vk();
                    if (!bridge || typeof bridge.send !== "function") throw self.error(1, "The host does not expose VK Bridge.");
                    var checkVkPlayer = function () {
                        if (self.vkProfile() !== profile) throw self.error(9, "The VK player changed.");
                    };
                    checkVkPlayer();
                    if (operation === "write") {
                        var result = await bridge.send("VKWebAppStorageSet", { key: key, value: document });
                        if (result && result.result === false) throw self.error(12, "VK Bridge rejected the save.");
                    }
                    var response = await bridge.send("VKWebAppStorageGet", { keys: [key] });
                    checkVkPlayer();
                    if (!response || !Array.isArray(response.keys)) throw self.error(12, "Invalid VK storage response.");
                    var entry = response.keys.find(function (item) { return item.key === key; });
                    var value = entry ? entry.value : "";
                    if (typeof value !== "string") throw self.error(7, "VK storage returned a non-string value.");
                    if (operation === "write" && value !== document)
                        throw self.error(10, "VK storage did not retain the complete save. Check the platform's value-size quota.");
                    return operation === "read" ? value : "";
                }
                throw self.error(1, "Unsupported storage backend.");
            });
            self.lanes[lane] = work;
            var release = function () { if (self.lanes[lane] === work) delete self.lanes[lane]; };
            // GamePush ready/get can expose its preferred local storage, without a new cloud read.
            work.then(function (value) { release(); self.reply(host, id, value, null, backend === "vk" || operation === "write"); },
                function (error) { release(); self.reply(host, id, "", error); });
        }
    },
    UniWebStorage_Request__deps: ["$UniWebStorage", "$UTF8ToString"],
    UniWebStorage_Request: function (host, id, backend, operation, key, profile, document, bindings) {
        UniWebStorage.run(UTF8ToString(host), UTF8ToString(id), UTF8ToString(backend),
            UTF8ToString(operation), UTF8ToString(key), UTF8ToString(profile),
            UTF8ToString(document), UTF8ToString(bindings));
    },
    UniWebStorage_VkAvailable__deps: ["$UniWebStorage"],
    UniWebStorage_VkAvailable: function () {
        var bridge = UniWebStorage.vk();
        return bridge && typeof bridge.send === "function" ? 1 : 0;
    },
    UniWebStorage_VkProfile__deps: ["$UniWebStorage", "$lengthBytesUTF8", "$stringToUTF8", "malloc"],
    UniWebStorage_VkProfile: function () {
        var value = UniWebStorage.vkProfile(), size = lengthBytesUTF8(value) + 1, result = _malloc(size);
        stringToUTF8(value, result, size); return result;
    }
});
