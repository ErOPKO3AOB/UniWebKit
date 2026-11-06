#if UNIWEB_PLUGIN_YG2
using System;
using YG;

namespace UniWebKit
{
    public class PluginYG2AdvProvider : IAdvProvider
    {
        public void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            void OnOpen()
            {
                YG2.onOpenInterAdv -= OnOpen;
                onOpened?.Invoke();
            }

            void OnClose()
            {
                YG2.onCloseInterAdv -= OnClose;
                YG2.onErrorInterAdv -= OnError;
                onClosed?.Invoke(true);
            }

            void OnError()
            {
                YG2.onErrorInterAdv -= OnError;
                YG2.onCloseInterAdv -= OnClose;
                onError?.Invoke("Ошибка показа Interstitial YG2");
            }

            YG2.onOpenInterAdv += OnOpen;
            YG2.onCloseInterAdv += OnClose;
            YG2.onErrorInterAdv += OnError;

            YG2.InterstitialAdvShow();
        }

        public void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
        {
            void OnOpen()
            {
                YG2.onOpenRewardedAdv -= OnOpen;
                onOpened?.Invoke();
            }

            void OnReward(string reward)
            {
                YG2.onRewardAdv -= OnReward;
                onReward?.Invoke();
            }

            void OnClose()
            {
                YG2.onCloseRewardedAdv -= OnClose;
                YG2.onErrorRewardedAdv -= OnError;

                onClosed?.Invoke(true);
            }

            void OnError()
            {
                YG2.onErrorRewardedAdv -= OnError;
                YG2.onCloseRewardedAdv -= OnClose;
                onError?.Invoke("Ошибка показа Rewarded YG2");
            }

            YG2.onOpenRewardedAdv += OnOpen;
            YG2.onRewardAdv += OnReward;
            YG2.onCloseRewardedAdv += OnClose;
            YG2.onErrorRewardedAdv += OnError;

            YG2.RewardedAdvShow("0");
        }
    }
}
#endif