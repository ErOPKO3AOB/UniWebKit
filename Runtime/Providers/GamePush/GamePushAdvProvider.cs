#if UNIWEB_GAMEPUSH
using GamePush;
using System;

namespace UniWebKit
{
    public class GamePushAdvProvider : IAdvProvider
    {
        public bool IsInterstitialReady => GP_Ads.IsFullscreenAvailable();

        public bool IsRewardedReady => GP_Ads.IsRewardedAvailable();

        public bool IsAdBlockEnabled => GP_Ads.IsAdblockEnabled();

        public void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            void OnClose(bool success)
            {
                if (!success)
                {
                    onError?.Invoke("Показ Interstitial в GamePush завершился неуспешно или отменён.");
                }
                onClosed?.Invoke(success);
            }

            GP_Ads.ShowFullscreen(onOpened, OnClose);
        }

        public void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null, string placementId = "default")
        {
            // GamePush передает строковый тег награды в колбэк
            void OnReward(string tag)
            {
                onReward?.Invoke();
            }

            void OnClose(bool success)
            {
                if (!success)
                {
                    onError?.Invoke("Показ Rewarded в GamePush завершился неуспешно или закрыт до награды.");
                }
                onClosed?.Invoke(success);
            }

            GP_Ads.ShowRewarded(placementId, OnReward, onOpened, OnClose);
        }

        public void ShowStickyBanner()
        {
            GP_Ads.ShowSticky();
        }

        public void HideStickyBanner()
        {
            GP_Ads.CloseSticky();
        }
    }
}
#endif