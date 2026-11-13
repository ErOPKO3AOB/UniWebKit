#if UNIWEB_PLUGIN_YG2
using System;
#if Localization_yg
using YG;
#endif

namespace UniWebKit
{
    public sealed class PluginYG2LocalizationProvider : LocalizationProviderBase
    {
#if Localization_yg
        private readonly IDisposable _initialization;

        public PluginYG2LocalizationProvider() : base(LocalLocalizationProvider.SystemLanguageCode(), false)
        {
            _initialization = LocalizationInitialization.WhenReady(() => YG2.isSDKEnabled, () =>
            {
                YG2.onSwitchLang += OnLanguageChanged;
                ApplyLanguage(YG2.lang);
            });
        }

        private void OnLanguageChanged(string language) => ApplyLanguage(language);

        public override bool SetLanguage(string language)
        {
            var code = NormalizeLanguage(language);
            if (IsDisposed || !IsReady || code == null) return false;
            if (Language != code) YG2.SwitchLanguage(code);
            return true;
        }

        public override void Dispose()
        {
            _initialization?.Dispose();
            YG2.onSwitchLang -= OnLanguageChanged;
            base.Dispose();
        }
#else
        // Without Localization_yg the feature remains usable locally.
        public PluginYG2LocalizationProvider() : base(LocalLocalizationProvider.SystemLanguageCode()) { }
#endif
    }
}
#endif
