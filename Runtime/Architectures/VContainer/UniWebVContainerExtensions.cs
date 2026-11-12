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
            builder.Register<IPlayerProvider>(_ => new LocalPrefsPlayerProvider(settings?.PlayerStorage), Lifetime.Singleton);
            if (settings == null) return;

            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.PluginYG2:
#if UNIWEB_PLUGIN_YG2
                    builder.Register<PluginYG2AdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
                    builder.Register<PluginYG2InAppProvider>(Lifetime.Singleton).As<IInAppProvider>();
                    builder.Register<PluginYG2EnvirProvider>(Lifetime.Singleton).As<IEnvirProvider>();
                    builder.Register<IPlayerProvider>(_ => new PluginYG2PlayerProvider(settings.PlayerStorage), Lifetime.Singleton);
#endif
                    break;
                case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEPUSH
                    builder.Register<GamePushAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
                    builder.Register<GamePushInAppProvider>(Lifetime.Singleton).As<IInAppProvider>();
                    builder.Register<GamePushEnvirProvider>(Lifetime.Singleton).As<IEnvirProvider>();
                    builder.Register<IPlayerProvider>(_ => new GamePushPlayerProvider(settings.PlayerStorage), Lifetime.Singleton);
#endif
                    break;
                case UniWebSettings.TargetPlatform.VKGames:
#if UNIWEB_VK_GAMES
                    builder.Register<IPlayerProvider>(_ => new VKGamesPlayerProvider(settings.PlayerStorage), Lifetime.Singleton);
#endif
                    break;
            }
        }
    }
}
#endif
