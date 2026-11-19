#if UNIWEB_GAMEPUSH
using GamePush;
using System;

namespace UniWebKit
{
    public class GamePushAdvProvider : AdvProviderBase
    {
        public override bool IsInterstitialReady => GP_Ads.IsFullscreenAvailable();

        public override bool IsRewardedReady => GP_Ads.IsRewardedAvailable();

        public override bool IsAdBlockEnabled => GP_Ads.IsAdblockEnabled();

        public override void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
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

        public override void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null, string placementId = "default")
        {
            bool rewarded = false;
            bool completed = false;

            void GrantReward()
            {
                if (rewarded) return;
                rewarded = true;
                onReward?.Invoke();
            }

            void OnReward(string _)
            {
                if (!completed) GrantReward();
            }

            void OnClose(bool success)
            {
                if (completed) return;
                completed = true;

                // GamePush can report a successful close before the separate reward event.
                bool earned = success || rewarded;
                try
                {
                    if (earned)
                        GrantReward();
                    else
                        onError?.Invoke("Показ Rewarded в GamePush завершился неуспешно или закрыт до награды.");
                }
                finally
                {
                    onClosed?.Invoke(earned);
                }
            }

#if UNITY_EDITOR
            bool opened = false;

            void OnOpened()
            {
                opened = true;
                onOpened?.Invoke();
            }

            GP_Ads.ShowRewarded(placementId, OnReward, OnOpened, OnClose);
            // The default Editor simulation invokes only the reward callback synchronously.
            if (rewarded && !opened) OnClose(true);
#else
            GP_Ads.ShowRewarded(placementId, OnReward, onOpened, OnClose);
#endif
        }

        public override void ShowStickyBanner()
        {
            GP_Ads.ShowSticky();
        }

        public override void HideStickyBanner()
        {
            GP_Ads.CloseSticky();
        }
    }
}
#endif