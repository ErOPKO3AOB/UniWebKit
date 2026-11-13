var UniWebVKBridge = {
    $vkState__deps: ['$lengthBytesUTF8', '$stringToUTF8', 'malloc', 'free'],
    $vkState: {
        initialized: false,
        initCompleted: false,
        bridge: null,
        initPromise: null,
        adBusy: false,
        baitStarted: false,
        ads: {
            interstitial: { ready: false, checking: false, nextCheck: 0 },
            reward: { ready: false, checking: false, nextCheck: 0 }
        },
        adBlockDetected: false,
        isPaused: false,
        listenersAttached: false,
        initCallbacks: [],
        pauseCallbackPtr: 0,

        withTimeout: function(promise, delay) {
            return new Promise(function(resolve, reject) {
                var timer = setTimeout(function() { reject(new Error('VK Bridge request timed out.')); }, delay);
                Promise.resolve(promise).then(function(value) {
                    clearTimeout(timer);
                    resolve(value);
                }, function(error) {
                    clearTimeout(timer);
                    reject(error);
                });
            });
        },

        supports: function(method) {
            return this.initCompleted && this.bridge && typeof this.bridge.send === 'function' &&
                (typeof this.bridge.supports !== 'function' || this.bridge.supports(method));
        },

        checkAd: function(format) {
            var state = this.ads[format];
            if (!state || this.adBusy || !this.supports('VKWebAppCheckNativeAds') ||
                state.checking || Date.now() < state.nextCheck) return;
            state.checking = true;
            state.nextCheck = Date.now() + 5000;
            var self = this;
            var request = Promise.resolve().then(function() {
                return self.bridge.send('VKWebAppCheckNativeAds', { ad_format: format });
            });
            this.withTimeout(request, 10000).then(function(data) {
                state.checking = false;
                state.ready = !self.adBusy && !!data && data.result === true;
            }, function() {
                state.checking = false;
                state.ready = false;
            });
        },

        // Выделение памяти в куче WASM для передачи строк в C# (string в MonoPInvokeCallback)
        allocString: function(str) {
            var safeStr = str || "";
            var bufferSize = lengthBytesUTF8(safeStr) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(safeStr, buffer, bufferSize);
            return buffer;
        },

        notifyInitCompleted: function(success) {
            this.initCompleted = success;
            while (this.initCallbacks.length > 0) {
                var cb = this.initCallbacks.shift();
                {{{ makeDynCall('vi', 'cb') }}}(success ? 1 : 0);
            }
        },

        setPaused: function(paused) {
            var nextState = !!paused;
            if (this.isPaused === nextState) return;
            this.isPaused = nextState;
            if (this.pauseCallbackPtr) {
                {{{ makeDynCall('vi', 'this.pauseCallbackPtr') }}}(this.isPaused ? 1 : 0);
            }
        },

        attachLifecycleListeners: function(bridge) {
            if (this.listenersAttached) return;
            this.listenersAttached = true;
            var self = this;

            // Нативные события перекрытия/восстановления окна от VK Bridge
            if (bridge && typeof bridge.subscribe === 'function') {
                bridge.subscribe(function(e) {
                    if (!e || !e.detail) return;
                    var type = e.detail.type;
                    if (type === 'VKWebAppViewHide') {
                        self.setPaused(true);
                    } else if (type === 'VKWebAppViewRestore') {
                        self.setPaused(false);
                    }
                });
            }

            // Fallback на сворачивание вкладки в десктопном браузере
            if (typeof document !== 'undefined') {
                document.addEventListener('visibilitychange', function() {
                    self.setPaused(document.hidden);
                });
            }
        },

        ensureBridge: function(callback) {
            var self = this;
            if (typeof window === 'undefined') { callback(null); return; }
            if (this.initCompleted) { callback(this.bridge); return; }
            if (!this.initPromise) {
                // Shared by all requests and Unity instances on the page. The template only loads the SDK.
                if (!window.__uniWebVKInitPromise) {
                    var loaded = new Promise(function(resolve, reject) {
                        function getBridge() {
                            var bridge = window.vkBridge;
                            return bridge && bridge.default ? bridge.default : bridge;
                        }
                        if (getBridge()) { resolve(getBridge()); return; }
                        if (typeof document === 'undefined') { reject(new Error('No document.')); return; }
                        var script = document.createElement('script');
                        script.src = 'https://unpkg.com/@vkontakte/vk-bridge/dist/browser.min.js';
                        script.onload = function() {
                            var bridge = getBridge();
                            if (bridge) resolve(bridge);
                            else reject(new Error('VK Bridge was not exported.'));
                        };
                        script.onerror = function() { reject(new Error('VK Bridge failed to load.')); };
                        document.head.appendChild(script);
                    });
                    var initialization = this.withTimeout(loaded, 15000).then(function(bridge) {
                        return self.withTimeout(bridge.send('VKWebAppInit'), 15000).then(function(data) {
                            if (!data || data.result !== true) throw new Error('VKWebAppInit was not confirmed.');
                            return bridge;
                        });
                    });
                    window.__uniWebVKInitPromise = initialization;
                    initialization.catch(function() {
                        if (window.__uniWebVKInitPromise === initialization) window.__uniWebVKInitPromise = null;
                    });
                }
                this.initPromise = window.__uniWebVKInitPromise.then(function(bridge) {
                    self.bridge = bridge;
                    self.initialized = true;
                    self.attachLifecycleListeners(bridge);
                    self.notifyInitCompleted(true);
                    return bridge;
                }, function(error) {
                    self.initialized = false;
                    self.initPromise = null;
                    self.notifyInitCompleted(false);
                    throw error;
                });
            }
            this.initPromise.then(callback, function() { callback(null); });
        }
    },

    // =========================================================================
    // 1. PLATFORM PROVIDER (Init & Ready state)
    // =========================================================================

    UniWebVK_Init: function() {
        vkState.ensureBridge(function(bridge) {
            if (!bridge) return;
            vkState.checkAd('interstitial');
            vkState.checkAd('reward');
        });

        if (vkState.baitStarted || typeof document === 'undefined') return;
        vkState.baitStarted = true;
        // Best-effort heuristic, not a guarantee that the platform has ad inventory.
        try {
            var bait = document.createElement('div');
            bait.className = 'pub_300x250 pub_300x250m pub_728x90 text-ad textAd text_ad text_ads text-ads text-ad-links ad-banner adsbox';
            bait.style.cssText = 'width: 1px !important; height: 1px !important; position: absolute !important; left: -10000px !important; top: -1000px !important;';
            document.body.appendChild(bait);
            setTimeout(function() {
                vkState.adBlockDetected = (bait.offsetParent === null || bait.offsetHeight === 0 || bait.offsetLeft === 0);
                if (bait.parentNode) bait.parentNode.removeChild(bait);
            }, 150);
        } catch (e) {
            vkState.adBlockDetected = false;
        }
    },

    UniWebVK_InitWithCallback__deps: ['UniWebVK_Init'],
    UniWebVK_InitWithCallback: function(onInitializedPtr) {
        if (vkState.initCompleted) {
            if (onInitializedPtr) {{{ makeDynCall('vi', 'onInitializedPtr') }}}(1);
            return;
        }
        if (onInitializedPtr) {
            vkState.initCallbacks.push(onInitializedPtr);
        }
        _UniWebVK_Init();
    },

    UniWebVK_IsSupported: function() {
        return vkState.supports('VKWebAppShowNativeAds') || vkState.supports('VKWebAppShowBannerAd') ? 1 : 0;
    },

    UniWebVK_IsAdReady__deps: ['$UTF8ToString'],
    UniWebVK_IsAdReady: function(formatPtr) {
        var format = UTF8ToString(formatPtr);
        var state = vkState.ads[format];
        if (!state || vkState.adBusy || !vkState.supports('VKWebAppShowNativeAds')) return 0;
        vkState.checkAd(format);
        return state.ready ? 1 : 0;
    },

    UniWebVK_IsInitialized: function() {
        return vkState.initCompleted ? 1 : 0;
    },

    // =========================================================================
    // 2. PAUSE PROVIDER (VKWebAppViewHide / VKWebAppViewRestore + Tab Visibility)
    // =========================================================================

    UniWebVK_IsPaused: function() {
        return vkState.isPaused ? 1 : 0;
    },

    UniWebVK_RegisterPauseCallback: function(onPauseChangedPtr) {
        vkState.pauseCallbackPtr = onPauseChangedPtr;
        vkState.ensureBridge(function() {});
    },

    // =========================================================================
    // 3. PROFILE PROVIDER (VKWebAppGetUserInfo)
    // =========================================================================

    UniWebVK_GetUserInfo: function(onSuccessPtr, onErrorPtr) {
        vkState.ensureBridge(function(bridge) {
            if (!bridge) {
                var noBridgePtr = vkState.allocString("VK Bridge is not available.");
                {{{ makeDynCall('vi', 'onErrorPtr') }}}(noBridgePtr);
                _free(noBridgePtr);
                return;
            }

            bridge.send('VKWebAppGetUserInfo')
                .then(function(data) {
                    var idStr = (data && data.id) ? String(data.id) : "";
                    var firstName = (data && data.first_name) ? data.first_name : "";
                    var lastName = (data && data.last_name) ? data.last_name : "";
                    var fullName = (firstName + " " + lastName).trim();
                    var avatarUrl = (data && (data.photo_200 || data.photo_100)) ? (data.photo_200 || data.photo_100) : "";

                    var idPtr = vkState.allocString(idStr);
                    var namePtr = vkState.allocString(fullName);
                    var avatarPtr = vkState.allocString(avatarUrl);

                    // Action<string, string, string> (id, fullName, avatarUrl)
                    {{{ makeDynCall('viii', 'onSuccessPtr') }}}(idPtr, namePtr, avatarPtr);

                    _free(idPtr);
                    _free(namePtr);
                    _free(avatarPtr);
                })
                .catch(function(error) {
                    var errMsg = (error && error.error_data && error.error_data.error_reason)
                        ? error.error_data.error_reason
                        : "VKWebAppGetUserInfo failed.";
                    var errPtr = vkState.allocString(errMsg);
                    {{{ makeDynCall('vi', 'onErrorPtr') }}}(errPtr);
                    _free(errPtr);
                });
        });
    },

    // =========================================================================
    // 4. IN-APP PROVIDER (VKWebAppShowOrderBox)
    // =========================================================================

    UniWebVK_PurchaseItem__deps: ['$UTF8ToString'],
    UniWebVK_PurchaseItem: function(itemIdPtr, onSuccessPtr, onErrorPtr) {
        var itemId = UTF8ToString(itemIdPtr);

        vkState.ensureBridge(function(bridge) {
            if (!bridge) {
                var noBridgePtr = vkState.allocString("VK Bridge is not available.");
                {{{ makeDynCall('vi', 'onErrorPtr') }}}(noBridgePtr);
                _free(noBridgePtr);
                return;
            }

            bridge.send('VKWebAppShowOrderBox', {
                type: 'item',
                item: itemId
            })
            .then(function(data) {
                if (data && (data.success === true || data.status === 'chargeable')) {
                    var orderId = data.order_id ? String(data.order_id) : itemId;
                    var itemOutPtr = vkState.allocString(itemId);
                    var orderOutPtr = vkState.allocString(orderId);

                    // Action<string, string> (itemId, orderId / purchaseToken)
                    {{{ makeDynCall('vii', 'onSuccessPtr') }}}(itemOutPtr, orderOutPtr);

                    _free(itemOutPtr);
                    _free(orderOutPtr);
                } else {
                    var cancelPtr = vkState.allocString("Purchase was not completed.");
                    {{{ makeDynCall('vi', 'onErrorPtr') }}}(cancelPtr);
                    _free(cancelPtr);
                }
            })
            .catch(function(error) {
                var errMsg = (error && error.error_data && error.error_data.error_reason)
                    ? error.error_data.error_reason
                    : "VKWebAppShowOrderBox rejected or failed.";
                var errPtr = vkState.allocString(errMsg);
                {{{ makeDynCall('vi', 'onErrorPtr') }}}(errPtr);
                _free(errPtr);
            });
        });
    },

    // =========================================================================
    // 5. ADV PROVIDER (Native Ads & Banner Ads)
    // =========================================================================

    UniWebVK_IsAdBlockEnabled: function() {
        return vkState.adBlockDetected ? 1 : 0;
    },

    UniWebVK_ShowNativeAds__deps: ['$UTF8ToString'],
    UniWebVK_ShowNativeAds: function(formatPtr, onOpenedPtr, onResultPtr, onErrorPtr) {
        var adFormat = UTF8ToString(formatPtr);
        var state = vkState.ads[adFormat];
        if (!vkState.initCompleted) {
            if (onErrorPtr) {{{ makeDynCall('vi', 'onErrorPtr') }}}(0);
            return;
        }
        if (!state || !state.ready || vkState.adBusy || !vkState.supports('VKWebAppShowNativeAds')) {
            if (onErrorPtr) {{{ makeDynCall('vi', 'onErrorPtr') }}}(1);
            return;
        }
        vkState.adBusy = true;
        state.ready = false;
        var completed = false;
        function finish(data, error) {
            if (completed) return;
            completed = true;
            vkState.adBusy = false;
            state.nextCheck = 0;
            vkState.checkAd(adFormat);
            if (error) {
                var code = error.error_data && error.error_data.error_code === 4 ? 1 : 2;
                if (onErrorPtr) {{{ makeDynCall('vi', 'onErrorPtr') }}}(code);
            } else if (onResultPtr) {
                {{{ makeDynCall('vi', 'onResultPtr') }}}(data && data.result === true ? 1 : 0);
            }
        }
        // No separate "opened" notification is supplied by this API: pause just before dispatch.
        if (onOpenedPtr) {{{ makeDynCall('v', 'onOpenedPtr') }}}();
        var request;
        try { request = vkState.bridge.send('VKWebAppShowNativeAds', { ad_format: adFormat }); }
        catch (error) { finish(null, error); return; }
        // Deliberately no ad timeout: an unknown result must not unlock another overlapping show.
        Promise.resolve(request).then(function(data) { finish(data, null); }, function(error) { finish(null, error); });
    },

    UniWebVK_ShowBannerAd__deps: ['$UTF8ToString'],
    UniWebVK_ShowBannerAd: function(locationPtr, layoutTypePtr) {
        var location = UTF8ToString(locationPtr);
        var layoutType = UTF8ToString(layoutTypePtr);

        vkState.ensureBridge(function(bridge) {
            if (!bridge || !vkState.supports('VKWebAppShowBannerAd')) return;
            Promise.resolve().then(function() {
                return bridge.send('VKWebAppShowBannerAd', { banner_location: location, layout_type: layoutType });
            }).catch(function() {});
        });
    },

    UniWebVK_HideBannerAd: function() {
        vkState.ensureBridge(function(bridge) {
            if (!bridge || !vkState.supports('VKWebAppHideBannerAd')) return;
            Promise.resolve().then(function() { return bridge.send('VKWebAppHideBannerAd'); }).catch(function() {});
        });
    }
};

autoAddDeps(UniWebVKBridge, '$vkState');
mergeInto(LibraryManager.library, UniWebVKBridge);
