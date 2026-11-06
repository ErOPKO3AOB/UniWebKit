#if UNIWEB_PLUGIN_YG2
using System;
using YG;

namespace UniWebKit
{
    public class PluginYG2AdvProvider : IAdvProvider
    {
        public void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            YG2.InterstitialAdvShow();
        }

        public void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            throw new NotImplementedException();
        }
    }
}
#endif