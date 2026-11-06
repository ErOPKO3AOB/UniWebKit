using System;

namespace UniWebKit
{
    public interface IAdvProvider
    {
        void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null);
        void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null);
    }
}
