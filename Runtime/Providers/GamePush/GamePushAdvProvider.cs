#if UNIWEB_GAMEPUSH
using GamePush;
using System;

namespace UniWebKit
{
    public class GamePushAdvProvider : IAdvProvider
    {
        public void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            GP_Ads.ShowFullscreen(onOpened, onClosed);
        }

        public void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            throw new NotImplementedException();
        }
    }
}
#endif