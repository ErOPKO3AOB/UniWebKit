#if UNIWEB_GAMEPUSH
using System;
using GamePush;

namespace UniWebKit
{
    public sealed class GamePushLocalizationProvider : LocalizationProviderBase
    {
        // Only the adapter knows the SDK's closed enum and its historical spelling.
        private static readonly string[] Codes =
            { "en", "ru", "tr", "fr", "it", "de", "es", "zh", "pt", "ko", "ja", "ar", "hi", "id" };
        private static readonly GamePush.Language[] Languages =
        {
            GamePush.Language.English, GamePush.Language.Russian, GamePush.Language.Turkish,
            GamePush.Language.French, GamePush.Language.Italian, GamePush.Language.German,
            GamePush.Language.Spanish, GamePush.Language.Chineese, GamePush.Language.Portuguese,
            GamePush.Language.Korean, GamePush.Language.Japanese, GamePush.Language.Arab,
            GamePush.Language.Hindi, GamePush.Language.Indonesian
        };
        private readonly IDisposable _initialization;

        public GamePushLocalizationProvider() : base(LocalLocalizationProvider.SystemLanguageCode(), false)
        {
            _initialization = LocalizationInitialization.WhenReady(() => GP_Init.isReady, () =>
            {
                GP_Language.OnChangeLanguage += OnLanguageChanged;
                OnLanguageChanged(GP_Language.Current());
            });
        }

        private void OnLanguageChanged(GamePush.Language language)
        {
            int index = Array.IndexOf(Languages, language);
            if (index >= 0) ApplyLanguage(Codes[index]);
        }

        public override bool SetLanguage(string language)
        {
            var code = NormalizeLanguage(language);
            if (IsDisposed || !IsReady || code == null) return false;
            int index = Array.IndexOf(Codes, code);
            // Do not silently turn an unsupported language or region into English.
            if (index < 0) return false;
            if (Language != code) GP_Language.Change(Languages[index]);
            return true;
        }

        public override void Dispose()
        {
            _initialization?.Dispose();
            GP_Language.OnChangeLanguage -= OnLanguageChanged;
            base.Dispose();
        }
    }
}
#endif
