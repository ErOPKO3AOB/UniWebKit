const UniWebVKBridge = {
    $vkState: {
        initialized: false,
        adBlockDetected: false,
        ensureBridge: function(callback) {
            if (typeof window.vkBridge !== 'undefined') {
                if (!this.initialized) {
                    this.initialized = true;
                    window.vkBridge.send('VKWebAppInit').catch(function() {});
                }
                callback(window.vkBridge);
                return;
            }

            var self = this;
            var script = document.createElement('script');
            script.src = 'https://unpkg.com/@vkontakte/vk-bridge/dist/browser.min.js';
            script.onload = function() {
                if (typeof window.vkBridge !== 'undefined') {
                    self.initialized = true;
                    window.vkBridge.send('VKWebAppInit').catch(function() {});
                    callback(window.vkBridge);
                } else {
                    callback(null);
                }
            };
            script.onerror = function() {
                callback(null);
            };
            document.head.appendChild(script);
        }
    },

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

    UniWebVK_IsSupported: function() {
        return typeof window !== 'undefined' ? 1 : 0;
    },

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