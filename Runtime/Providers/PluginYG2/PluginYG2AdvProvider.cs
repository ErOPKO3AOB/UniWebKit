#if UNIWEB_PLUGIN_YG2
using System;
using YG;

namespace UniWebKit
{
    public class PluginYG2AdvProvider : AdvProviderBase
    {
#if InterstitialAdv_yg || RewardedAdv_yg || StickyAdv_yg
        public override bool IsSupported => true;
#else
        public override bool IsSupported => false;
#endif

#if InterstitialAdv_yg
        // Проверяем кулдаун таймера фулскрин рекламы в YG2 (и что сейчас не открыта другая реклама)
        public override bool IsInterstitialReady => YG2.isTimerAdvCompleted && !YG2.nowAdsShow;

        public override void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            void OnOpen()
            {
                YG2.onOpenInterAdv -= OnOpen;
                onOpened?.Invoke();
            }

            void OnClose()
            {
                Cleanup();
                onClosed?.Invoke(true);
            }

            void OnError()
            {
                Cleanup();
                onError?.Invoke("Ошибка показа Interstitial YG2");
                onClosed?.Invoke(false);
            }

            void Cleanup()
            {
                YG2.onOpenInterAdv -= OnOpen;
                YG2.onCloseInterAdv -= OnClose;
                YG2.onErrorInterAdv -= OnError;
            }

            YG2.onOpenInterAdv += OnOpen;
            YG2.onCloseInterAdv += OnClose;
            YG2.onErrorInterAdv += OnError;

            YG2.InterstitialAdvShow();
        }
#endif

#if RewardedAdv_yg
        // Яндекс не отдает статус предзагрузки реварда заранее, проверяем только отсутствие активной рекламы
        public override bool IsRewardedReady => !YG2.nowAdsShow;

        public override void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null, string placementId = "default")
        {
            void OnOpen()
            {
                YG2.onOpenRewardedAdv -= OnOpen;
                onOpened?.Invoke();
            }

            void OnReward(string id)
            {
                if (id == placementId)
                {
                    YG2.onRewardAdv -= OnReward;
                    onReward?.Invoke();
                }
            }

            void OnClose()
            {
                Cleanup();
                onClosed?.Invoke(true);
            }

            void OnError()
            {
                Cleanup();
                onError?.Invoke("Ошибка показа Rewarded YG2");
                onClosed?.Invoke(false);
            }

            void Cleanup()
            {
                YG2.onOpenRewardedAdv -= OnOpen;
                YG2.onRewardAdv -= OnReward;
                YG2.onCloseRewardedAdv -= OnClose;
                YG2.onErrorRewardedAdv -= OnError;
            }

            YG2.onOpenRewardedAdv += OnOpen;
            YG2.onRewardAdv += OnReward;
            YG2.onCloseRewardedAdv += OnClose;
            YG2.onErrorRewardedAdv += OnError;

            YG2.RewardedAdvShow(placementId);
        }
#endif

#if StickyAdv_yg
        public override void ShowStickyBanner()
        {
            YG2.StickyAdActivity(true);
        }

        public override void HideStickyBanner()
        {
            YG2.StickyAdActivity(false);
        }
#endif
    }
}
#endif
