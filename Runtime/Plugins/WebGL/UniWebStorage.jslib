mergeInto(LibraryManager.library, {
    $UniWebStorage: {
        lanes: {},
        error: function (code, message) { return { storageCode: code, message: message }; },
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
        run: function (host, id, key, profile, cloudConfirmed, request) {
            var self = UniWebStorage;
            var lane = profile + "|" + key;
            var previous = self.lanes[lane] || Promise.resolve();
            // A timed-out managed request can still be in flight in the SDK.
            var work = previous.catch(function () {}).then(function () {
                if (!/^[a-zA-Z0-9_-]+$/.test(key))
                    throw self.error(8, "Cloud keys must contain only letters, digits, underscores and hyphens.");
                return request();
            });
            self.lanes[lane] = work;
            var release = function () { if (self.lanes[lane] === work) delete self.lanes[lane]; };
            work.then(function (value) { release(); self.reply(host, id, value, null, cloudConfirmed); },
                function (error) { release(); self.reply(host, id, "", error); });
        }
    }
});
