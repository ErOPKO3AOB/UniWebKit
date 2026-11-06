using System;

namespace UniWebKit
{
    // --- 1. Базовый абстрактный класс (чтобы не писать пустые методы) ---
    public abstract class AdvProviderBase : IAdvProvider
    {
        // По умолчанию считаем, что если класс создан, то фича поддерживается
        public virtual bool IsSupported => true;

        public virtual bool IsInterstitialReady => false;
        public virtual bool IsRewardedReady => false;
        public virtual bool IsAdBlockEnabled => false;

        // Виртуальные пустышки: если провайдер не переопределит метод, 
        // он просто безопасно отработает и триггернет колбэк закрытия
        public virtual void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
            => onClosed?.Invoke(false);

        public virtual void ShowRewarded(Action onReward, Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null, string placementId = "default")
            => onClosed?.Invoke(false);

        public virtual void ShowStickyBanner() { }
        public virtual void HideStickyBanner() { }
    }
}