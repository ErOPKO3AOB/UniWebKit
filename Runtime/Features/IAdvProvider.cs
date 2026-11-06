using System;

namespace UniWebKit
{
    public interface IAdvProvider
    {
        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }
        bool IsAdBlockEnabled { get; }

        void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null);
        void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null, string placementId = "default");

        void ShowStickyBanner();
        void HideStickyBanner();
    }
}