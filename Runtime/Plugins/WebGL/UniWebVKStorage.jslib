mergeInto(LibraryManager.library, {
    $UniWebVKStorage__deps: ["$UniWebStorage"],
    $UniWebVKStorage: {
        vk: function () {
            var bridge = typeof window !== "undefined" ? window.vkBridge : null;
            return bridge && bridge.default ? bridge.default : bridge;
        },
        vkProfile: function () {
            try { return new URLSearchParams(window.location.search).get("vk_user_id") || "guest"; }
            catch (_) { return "guest"; }
        },
        request: async function (operation, key, profile, document) {
            var self = UniWebVKStorage;
            var bridge = self.vk();
            if (!bridge || typeof bridge.send !== "function") throw UniWebStorage.error(1, "The host does not expose VK Bridge.");
            var checkVkPlayer = function () {
                if (self.vkProfile() !== profile) throw UniWebStorage.error(9, "The VK player changed.");
            };
            checkVkPlayer();
            if (operation === "write") {
                var result = await bridge.send("VKWebAppStorageSet", { key: key, value: document });
                if (result && result.result === false) throw UniWebStorage.error(12, "VK Bridge rejected the save.");
            }
            var response = await bridge.send("VKWebAppStorageGet", { keys: [key] });
            checkVkPlayer();
            if (!response || !Array.isArray(response.keys)) throw UniWebStorage.error(12, "Invalid VK storage response.");
            var entry = response.keys.find(function (item) { return item.key === key; });
            var value = entry ? entry.value : "";
            if (typeof value !== "string") throw UniWebStorage.error(7, "VK storage returned a non-string value.");
            if (operation === "write" && value !== document)
                throw UniWebStorage.error(10, "VK storage did not retain the complete save. Check the platform's value-size quota.");
            return operation === "read" ? value : "";
        }
    },
    UniWebStorage_Request__deps: ["$UniWebStorage", "$UniWebVKStorage", "$UTF8ToString"],
    UniWebStorage_Request: function (host, id, backend, operation, key, profile, document, bindings) {
        var operationName = UTF8ToString(operation), keyName = UTF8ToString(key);
        var profileId = UTF8ToString(profile), value = UTF8ToString(document);
        UniWebStorage.run(UTF8ToString(host), UTF8ToString(id), keyName, profileId, true,
            function () { return UniWebVKStorage.request(operationName, keyName, profileId, value); });
    },
    UniWebStorage_VkAvailable__deps: ["$UniWebVKStorage"],
    UniWebStorage_VkAvailable: function () {
        var bridge = UniWebVKStorage.vk();
        return bridge && typeof bridge.send === "function" ? 1 : 0;
    },
    UniWebStorage_VkProfile__deps: ["$UniWebVKStorage", "$lengthBytesUTF8", "$stringToUTF8", "malloc"],
    UniWebStorage_VkProfile: function () {
        var value = UniWebVKStorage.vkProfile(), size = lengthBytesUTF8(value) + 1, result = _malloc(size);
        stringToUTF8(value, result, size); return result;
    }
});

