using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace UniWebKit
{
#if UNIWEB_VK_GAMES
    public sealed class VKGamesAdvProvider : AdvProviderBase
    {
        [DllImport("__Internal")]
        private static extern void UniWebVK_Init();

        [DllImport("__Internal")]
        private static extern int UniWebVK_IsSupported();

        [DllImport("__Internal")]
        private static extern int UniWebVK_IsAdBlockEnabled();

        [DllImport("__Internal")]
        private static extern void UniWebVK_ShowNativeAds(
            string adFormat,
            Action onOpened,
            Action<int> onResult,
            Action<int> onError);

        [DllImport("__Internal")]
        private static extern void UniWebVK_ShowBannerAd(string bannerLocation, string layoutType);

        [DllImport("__Internal")]
        private static extern void UniWebVK_HideBannerAd();

        private static VKGamesAdvProvider _activeInstance;

        private Action _currentOnOpened;
        private Action _currentOnRewarded;
        private Action<bool> _currentOnClosed;
        private Action<string> _currentOnError;
        private bool _isRewardedFlow;
        private bool _isBusy;

        public override bool IsSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return UniWebVK_IsSupported() == 1;
#else
                return false;
#endif
            }
        }

        public override bool IsAdBlockEnabled
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return UniWebVK_IsAdBlockEnabled() == 1;
#else
                return false;
#endif
            }
        }

        public VKGamesAdvProvider()
        {
            _activeInstance = this;
#if UNITY_WEBGL && !UNITY_EDITOR
            UniWebVK_Init();
#endif
        }

        public override void ShowInterstitial(
            Action onOpened = null,
            Action<bool> onClosed = null,
            Action<string> onError = null,
            string placementId = "default")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_isBusy)
            {
                onError?.Invoke("Advertisement is already showing.");
                onClosed?.Invoke(false);
                return;
            }

            _isBusy = true;
            _isRewardedFlow = false;
            _currentOnOpened = onOpened;
            _currentOnRewarded = null;
            _currentOnClosed = onClosed;
            _currentOnError = onError;

            UniWebVK_ShowNativeAds("interstitial", OnAdOpenedCallback, OnAdResultCallback, OnAdErrorCallback);
#else
            base.ShowInterstitial(onOpened, onClosed, onError, placementId);
#endif
        }

        public override void ShowRewarded(
            Action onRewarded,
            Action onOpened = null,
            Action<bool> onClosed = null,
            Action<string> onError = null,
            string placementId = "default")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_isBusy)
            {
                onError?.Invoke("Advertisement is already showing.");
                onClosed?.Invoke(false);
                return;
            }

            _isBusy = true;
            _isRewardedFlow = true;
            _currentOnOpened = onOpened;
            _currentOnRewarded = onRewarded;
            _currentOnClosed = onClosed;
            _currentOnError = onError;

            UniWebVK_ShowNativeAds("reward", OnAdOpenedCallback, OnAdResultCallback, OnAdErrorCallback);
#else
            base.ShowRewarded(onRewarded, onOpened, onClosed, onError, placementId);
#endif
        }

        public override void ShowStickyBanner()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // bottom + resize сдвигает вьюпорт игры, чтобы баннер не перекрывал UI
            UniWebVK_ShowBannerAd("bottom", "resize");
#else
            base.ShowStickyBanner();
#endif
        }

        public override void HideStickyBanner()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            UniWebVK_HideBannerAd();
#else
            base.HideStickyBanner();
#endif
        }

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnAdOpenedCallback()
        {
            if (_activeInstance == null) return;
            AudioListener.pause = true;
            _activeInstance._currentOnOpened?.Invoke();
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnAdResultCallback(int success)
        {
            if (_activeInstance == null) return;

            AudioListener.pause = false;
            bool isSuccess = success == 1;

            var onRewarded = _activeInstance._currentOnRewarded;
            var onClosed = _activeInstance._currentOnClosed;
            var onError = _activeInstance._currentOnError;
            bool isRewarded = _activeInstance._isRewardedFlow;

            _activeInstance.ClearCallbacks();

            if (isSuccess)
            {
                if (isRewarded)
                {
                    onRewarded?.Invoke();
                }
                onClosed?.Invoke(true);
            }
            else
            {
                onError?.Invoke("VK ad was not completed or returned false.");
                onClosed?.Invoke(false);
            }
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnAdErrorCallback(int errorCode)
        {
            if (_activeInstance == null) return;

            AudioListener.pause = false;

            var onClosed = _activeInstance._currentOnClosed;
            var onError = _activeInstance._currentOnError;

            _activeInstance.ClearCallbacks();

            string reason = errorCode switch
            {
                0 => "VK Bridge is not initialized.",
                1 => "No ad inventory available or rejected by user.",
                _ => "VKWebAppShowNativeAds failed."
            };

            onError?.Invoke(reason);
            onClosed?.Invoke(false);
        }

        private void ClearCallbacks()
        {
            _isBusy = false;
            _currentOnOpened = null;
            _currentOnRewarded = null;
            _currentOnClosed = null;
            _currentOnError = null;
        }

        public override void Dispose()
        {
            ClearCallbacks();
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
            base.Dispose();
        }
    }
#endif
}