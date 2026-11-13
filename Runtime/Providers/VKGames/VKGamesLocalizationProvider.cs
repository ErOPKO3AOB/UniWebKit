#if UNIWEB_VK_GAMES
namespace UniWebKit
{
    public sealed class VKGamesLocalizationProvider : LocalizationProviderBase
    {
        private readonly VKGamesRuntime _runtime;
        private bool _selectedByGame;

        public VKGamesLocalizationProvider() : base(VKGamesRuntime.Language)
        {
            if (!VKGamesRuntime.IsBrowser) return;
            _runtime = VKGamesRuntime.Instance;
            _runtime.EnvironmentChanged += SynchronizeLanguage;
            SynchronizeLanguage();
        }

        private void SynchronizeLanguage()
        {
            if (!IsDisposed && !_selectedByGame) ApplyLanguage(VKGamesRuntime.Language);
        }

        public override bool SetLanguage(string language)
        {
            if (IsDisposed || NormalizeLanguage(language) == null) return false;
            // VK does not provide a setter for its interface language. This is the game's selection.
            _selectedByGame = true;
            return base.SetLanguage(language);
        }

        public override void Dispose()
        {
            if (_runtime != null) _runtime.EnvironmentChanged -= SynchronizeLanguage;
            base.Dispose();
        }
    }
}
#endif
