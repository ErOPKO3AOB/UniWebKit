#if UNIWEB_ARCHITECTURE_STATIC
using UnityEngine;

// ... юзинги платформ

namespace UniWebKit
{
    public static class UniWebBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            var settings = Resources.Load<UniWebSettings>("UniWebSettings");
            if (settings == null || !settings.AutoInitializeStaticAPI) return;

            // 1. СТЕЛЕМ СОЛОМКУ: Создаем безопасные "глухие" реализации по умолчанию
            IAdvProvider ads = new UnsupportedAdvProvider();
            IInAppProvider inApps = new UnsupportedInAppProvider();
            IEnvirProvider envir = new UnityEnvirProvider();
            ILocalizationProvider localization = new LocalLocalizationProvider();
            ILeaderboardProvider leaderboards = new UnsupportedLeaderboardProvider();
            IPlayerProvider player = new LocalPrefsPlayerProvider(settings.PlayerStorage);
            IPlatformProvider platform = new UnsupportedPlatformProvider();
            IProfileProvider profile = new UnsupportedProfileProvider();
            IGameProvider game = new UnsupportedGameProvider();
            IPauseProvider pause = new LocalPauseProvider();

            // 2. ПЕРЕЗАПИСЫВАЕМ (если платформа это умеет)
            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.PluginYG2:
#if UNIWEB_PLUGIN_YG2
                    ads = new PluginYG2AdvProvider();
                    inApps = new PluginYG2InAppProvider();
                    envir = new PluginYG2EnvirProvider();
                    localization = new PluginYG2LocalizationProvider();
                    player = new PluginYG2PlayerProvider(settings.PlayerStorage);
#endif
                    break;

                case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEPUSH
                    ads = new GamePushAdvProvider();
                    inApps = new GamePushInAppProvider();
                    envir = new GamePushEnvirProvider();
                    localization = new GamePushLocalizationProvider();
                    player = new GamePushPlayerProvider(settings.PlayerStorage);
#endif
                    break;
                case UniWebSettings.TargetPlatform.VKGames:
#if UNIWEB_VK_GAMES
                    platform = new VKGamesPlatformProvider();
                    profile = new VKGamesProfileProvider();
                    pause = new VKGamesPauseProvider();
                    ads = new VKGamesAdvProvider();
                    inApps = new VKGamesInAppProvider();
                    player = new VKGamesPlayerProvider(settings.PlayerStorage);
#endif
                    break;
            }

            // 3. Отправляем в мост
            UniWebBridge.Initialize(ads, inApps, envir, player, localization, leaderboards, platform, profile, game, pause);
        }
    }
}
#endif
