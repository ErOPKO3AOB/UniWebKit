#if UNIWEB_VK_GAMES
using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace UniWebKit
{
    public sealed class VKGamesAdvProvider : AdvProviderBase, IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void AdOpenedCallback();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void AdResultCallback(int value);
        // One native ad at a time across every instance, including disposed instances awaiting a result.
        private sealed class PendingAd
        {
            internal VKGamesAdvProvider Owner;
            internal Action Opened, Reward;
            internal Action<bool> Closed;
            internal Action<string> Error;
            internal bool Muted, PreviousAudioPause, OpenedNotified;
        }

        private static PendingAd _pending;
        // A resolved VK pause provider hands time/audio policy to the game's pause controller.
        internal static int PauseManagers;
        private bool _disposed;

        // Keep reverse-P/Invoke delegates alive for the complete lifetime of the WebGL module.
        private static readonly AdOpenedCallback OpenedCallback = OnAdOpened;
        private static readonly AdResultCallback ResultCallback = OnAdResult;
        private static readonly AdResultCallback ErrorCallback = OnAdError;

        public VKGamesAdvProvider()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            UniWebVK_Init();
#endif
        }

        public override bool IsSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return !_disposed && UniWebVK_IsSupported() == 1;
#else
                return false;
#endif
            }
        }

        public override bool IsInterstitialReady => IsReady("interstitial");
        public override bool IsRewardedReady => IsReady("reward");

        public override bool IsAdBlockEnabled
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return !_disposed && UniWebVK_IsAdBlockEnabled() == 1;
#else
                return false;
#endif
            }
        }

        private bool IsReady(string format)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return !_disposed && _pending == null && UniWebVK_IsAdReady(format) == 1;
#else
            return false;
#endif
        }

        public override void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null,
            Action<string> onError = null)
            => Show("interstitial", null, onOpened, onClosed, onError);

        public override void ShowRewarded(Action onReward, Action onOpened = null,
            Action<bool> onClosed = null, Action<string> onError = null, string placementId = "default")
            => Show("reward", onReward, onOpened, onClosed, onError);

        private void Show(string format, Action reward, Action opened, Action<bool> closed, Action<string> error)
        {
            bool canRequest = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            // The inventory check is advisory for an explicit rewarded request.
            canRequest = !_disposed && _pending == null && (format == "reward" || IsReady(format));
#endif
            if (!canRequest)
            {
                Invoke(() => error?.Invoke(_disposed ? "The VK ads provider has been disposed."
                    : _pending != null ? "Another VK advertisement is still pending."
                    : "VK advertisement is not ready or is unavailable on this platform."));
                Invoke(() => closed?.Invoke(false));
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            var pending = new PendingAd { Owner = this, Opened = opened, Reward = reward, Closed = closed, Error = error };
            _pending = pending;
            try { UniWebVK_ShowNativeAds(format, OpenedCallback, ResultCallback, ErrorCallback); }
            catch (Exception exception)
            {
                // Synchronous callbacks may already have completed this operation and started another.
                if (ReferenceEquals(_pending, pending)) Complete(false, exception.Message);
                else Debug.LogException(exception);
            }
#endif
        }

        public override void ShowStickyBanner()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_disposed) UniWebVK_ShowBannerAd("bottom", "resize");
#endif
        }

        public override void HideStickyBanner()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_disposed) UniWebVK_HideBannerAd();
#endif
        }

        [MonoPInvokeCallback(typeof(AdOpenedCallback))]
        private static void OnAdOpened()
        {
            var pending = _pending;
            if (pending == null || pending.Owner == null || pending.OpenedNotified) return;
            pending.OpenedNotified = true;
            if (PauseManagers == 0)
            {
                pending.PreviousAudioPause = AudioListener.pause;
                pending.Muted = true;
                AudioListener.pause = true;
            }
            Invoke(pending.Opened);
        }

        [MonoPInvokeCallback(typeof(AdResultCallback))]
        private static void OnAdResult(int success)
            => Complete(success == 1, success == 1 ? null : "VK advertisement did not complete successfully.");

        [MonoPInvokeCallback(typeof(AdResultCallback))]
        private static void OnAdError(int code)
            => Complete(false, code == 0 ? "VK Bridge is not initialized."
                : code == 1 ? "VK advertisement is unavailable or another ad is pending."
                : "VKWebAppShowNativeAds failed.");

        private static void Complete(bool success, string message)
        {
            var pending = _pending;
            if (pending == null) return;
            _pending = null;
            RestoreAudio(pending);
            if (success) Invoke(pending.Reward);
            else Invoke(() => pending.Error?.Invoke(message));
            Invoke(() => pending.Closed?.Invoke(success));
        }

        private static void RestoreAudio(PendingAd pending)
        {
            if (!pending.Muted) return;
            pending.Muted = false;
            if (PauseManagers == 0) AudioListener.pause = pending.PreviousAudioPause;
        }

        private static void Invoke(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var pending = _pending;
            if (pending == null || !ReferenceEquals(pending.Owner, this)) return;
            var error = pending.Error;
            var closed = pending.Closed;
            // The SDK cannot cancel an ad. Drain its result without retaining the game or rewarding it later.
            pending.Owner = null;
            pending.Opened = pending.Reward = null;
            pending.Error = null;
            pending.Closed = null;
            RestoreAudio(pending);
            Invoke(() => error?.Invoke("The VK ads provider was disposed while awaiting the advertisement."));
            Invoke(() => closed?.Invoke(false));
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void UniWebVK_Init();
        [DllImport("__Internal")] private static extern int UniWebVK_IsSupported();
        [DllImport("__Internal")] private static extern int UniWebVK_IsAdReady(string format);
        [DllImport("__Internal")] private static extern int UniWebVK_IsAdBlockEnabled();
        [DllImport("__Internal")]
        private static extern void UniWebVK_ShowNativeAds(string format, AdOpenedCallback opened, AdResultCallback result, AdResultCallback error);
        [DllImport("__Internal")] private static extern void UniWebVK_ShowBannerAd(string location, string layout);
        [DllImport("__Internal")] private static extern void UniWebVK_HideBannerAd();
#endif
    }
}
#endif
