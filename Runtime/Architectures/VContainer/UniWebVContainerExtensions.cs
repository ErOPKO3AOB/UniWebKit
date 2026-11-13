#if UNIWEB_ARCHITECTURE_VCONTAINER
using VContainer;
using UnityEngine;

namespace UniWebKit
{
    public static class UniWebVContainerExtensions
    {
        public static void RegisterUniWeb(this IContainerBuilder builder)
        {
            var settings = Resources.Load<UniWebSettings>("UniWebSettings");
            // Shadow fallbacks remain valid even if the settings asset is absent.
            builder.Register<UnsupportedAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
            builder.Register<UnsupportedInAppProvider>(Lifetime.Singleton).As<IInAppProvider>();
            builder.Register<UnityEnvirProvider>(Lifetime.Singleton).As<IEnvirProvider>();
            builder.Register<LocalLocalizationProvider>(Lifetime.Singleton).As<ILocalizationProvider>();
            builder.Register<UnsupportedLeaderboardProvider>(Lifetime.Singleton).As<ILeaderboardProvider>();
            builder.Register<UnsupportedPlatformProvider>(Lifetime.Singleton).As<IPlatformProvider>();
            builder.Register<UnsupportedProfileProvider>(Lifetime.Singleton).As<IProfileProvider>();
            builder.Register<UnsupportedGameProvider>(Lifetime.Singleton).As<IGameProvider>();
            builder.Register<LocalPauseProvider>(Lifetime.Singleton).As<IPauseProvider>();
            builder.Register<IPlayerProvider>(_ => new LocalPrefsPlayerProvider(settings?.PlayerStorage), Lifetime.Singleton);
            if (settings == null) return;

            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.PluginYG2:
#if UNIWEB_PLUGIN_YG2
                    builder.Register<PluginYG2AdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
                    builder.Register<PluginYG2InAppProvider>(Lifetime.Singleton).As<IInAppProvider>();
                    builder.Register<PluginYG2EnvirProvider>(Lifetime.Singleton).As<IEnvirProvider>();
                    builder.Register<PluginYG2LocalizationProvider>(Lifetime.Singleton).As<ILocalizationProvider>();
                    builder.Register<IPlayerProvider>(_ => new PluginYG2PlayerProvider(settings.PlayerStorage), Lifetime.Singleton);
#endif
                    break;
                case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEPUSH
                    builder.Register<GamePushAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
                    builder.Register<GamePushInAppProvider>(Lifetime.Singleton).As<IInAppProvider>();
                    builder.Register<GamePushEnvirProvider>(Lifetime.Singleton).As<IEnvirProvider>();
                    builder.Register<GamePushLocalizationProvider>(Lifetime.Singleton).As<ILocalizationProvider>();
                    builder.Register<IPlayerProvider>(_ => new GamePushPlayerProvider(settings.PlayerStorage), Lifetime.Singleton);
#endif
                    break;
                case UniWebSettings.TargetPlatform.VKGames:
#if UNIWEB_VK_GAMES
                    builder.Register<VKGamesPlatformProvider>(Lifetime.Singleton).As<IPlatformProvider>();
                    builder.Register<VKGamesProfileProvider>(Lifetime.Singleton).As<IProfileProvider>();
                    builder.Register<VKGamesPauseProvider>(Lifetime.Singleton).As<IPauseProvider>();
                    builder.Register<VKGamesInAppProvider>(Lifetime.Singleton).As<IInAppProvider>();
                    builder.Register<VKGamesAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
                    builder.Register<IPlayerProvider>(_ => new VKGamesPlayerProvider(settings.PlayerStorage), Lifetime.Singleton);
#endif
                    break;
            }
        }
    }
}
#endif
