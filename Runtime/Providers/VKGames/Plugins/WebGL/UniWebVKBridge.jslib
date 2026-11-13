const UniWebVKBridge = {
    $vkState: {
        initialized: false,
        initCompleted: false,
        adBlockDetected: false,
        isPaused: false,
        listenersAttached: false,
        initCallbacks: [],
        pauseCallbackPtr: 0,

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
            if (typeof window.vkBridge !== 'undefined') {
                if (!this.initialized) {
                    this.initialized = true;
                    this.attachLifecycleListeners(window.vkBridge);
                    window.vkBridge.send('VKWebAppInit')
                        .then(function() { self.notifyInitCompleted(true); })
                        .catch(function() { self.notifyInitCompleted(true); }); // В дев-окружении вне iframe ВК резолвим штатно
                }
                callback(window.vkBridge);
                return;
            }

            var script = document.createElement('script');
            script.src = 'https://unpkg.com/@vkontakte/vk-bridge/dist/browser.min.js';
            script.onload = function() {
                if (typeof window.vkBridge !== 'undefined') {
                    self.initialized = true;
                    self.attachLifecycleListeners(window.vkBridge);
                    window.vkBridge.send('VKWebAppInit')
                        .then(function() { self.notifyInitCompleted(true); })
                        .catch(function() { self.notifyInitCompleted(true); });
                    callback(window.vkBridge);
                } else {
                    self.notifyInitCompleted(false);
                    callback(null);
                }
            };
            script.onerror = function() {
                self.notifyInitCompleted(false);
                callback(null);
            };
            document.head.appendChild(script);
        }
    },

    // =========================================================================
    // 1. PLATFORM PROVIDER (Init & Ready state)
    // =========================================================================

    UniWebVK_Init: function() {
        vkState.ensureBridge(function(bridge) {
            if (!bridge) return;
            // Предзагрузка Interstitial и Rewarded
            bridge.send('VKWebAppCheckNativeAds', { ad_format: 'interstitial' }).catch(function() {});
            bridge.send('VKWebAppCheckNativeAds', { ad_format: 'reward' }).catch(function() {});
        });

        // Проверка AdBlock через фейковый рекламный контейнер
        try {
            var bait = document.createElement('div');
            bait.className = 'pub_300x250 pub_300x250m pub_728x90 text-ad textAd text_ad text_ads text-ads text-ad-links ad-banner adsbox';
            bait.style.cssText = 'width: 1px !important; height: 1px !important; position: absolute !important; left: -10000px !important; top: -1000px !important;';
            document.body.appendChild(bait);
            setTimeout(function() {
                vkState.adBlockDetected = (bait.offsetParent === null || bait.offsetHeight === 0 || bait.offsetLeft === 0);
                document.body.removeChild(bait);
            }, 150);
        } catch (e) {
            vkState.adBlockDetected = false;
        }
    },

    UniWebVK_InitWithCallback: function(onInitializedPtr) {
        if (vkState.initCompleted) {
            {{{ makeDynCall('vi', 'onInitializedPtr') }}}(1);
            return;
        }
        if (onInitializedPtr) {
            vkState.initCallbacks.push(onInitializedPtr);
        }
        _UniWebVK_Init();
    },

    UniWebVK_IsSupported: function() {
        return typeof window !== 'undefined' ? 1 : 0;
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

    UniWebVK_ShowNativeAds: function(formatPtr, onOpenedPtr, onResultPtr, onErrorPtr) {
        var adFormat = UTF8ToString(formatPtr);

        vkState.ensureBridge(function(bridge) {
            if (!bridge) {
                {{{ makeDynCall('vi', 'onErrorPtr') }}}(0); // 0 = Bridge not found
                return;
            }

            {{{ makeDynCall('v', 'onOpenedPtr') }}}();

            bridge.send('VKWebAppShowNativeAds', { ad_format: adFormat })
                .then(function(data) {
                    if (data && data.result === true) {
                        {{{ makeDynCall('vi', 'onResultPtr') }}}(1);
                    } else {
                        {{{ makeDynCall('vi', 'onResultPtr') }}}(0);
                    }
                    // Фоновый прогрев следующего рекламного блока
                    bridge.send('VKWebAppCheckNativeAds', { ad_format: adFormat }).catch(function() {});
                })
                .catch(function(error) {
                    // Коды: 1 = No ads / rejected, 2 = Network / API error
                    var code = (error && error.error_data && error.error_data.error_code === 4) ? 1 : 2;
                    {{{ makeDynCall('vi', 'onErrorPtr') }}}(code);
                });
        });
    },

    UniWebVK_ShowBannerAd: function(locationPtr, layoutTypePtr) {
        var location = UTF8ToString(locationPtr);
        var layoutType = UTF8ToString(layoutTypePtr);

        vkState.ensureBridge(function(bridge) {
            if (!bridge) return;
            bridge.send('VKWebAppShowBannerAd', {
                banner_location: location,
                layout_type: layoutType
            }).catch(function() {});
        });
    },

    UniWebVK_HideBannerAd: function() {
        vkState.ensureBridge(function(bridge) {
            if (!bridge) return;
            bridge.send('VKWebAppHideBannerAd').catch(function() {});
        });
    }
};

autoAddDeps(UniWebVKBridge, '$vkState');
mergeInto(LibraryManager.library, UniWebVKBridge);