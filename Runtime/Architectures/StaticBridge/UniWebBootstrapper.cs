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
            //ILeaderboardProvider leaderboards = new UnsupportedLeaderboardProvider();
            IPlayerProvider player = new LocalPrefsPlayerProvider(settings.PlayerStorage);

            // 2. ПЕРЕЗАПИСЫВАЕМ (если платформа это умеет)
            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.PluginYG2:
#if UNIWEB_PLUGIN_YG2
                    ads = new PluginYG2AdvProvider();
                    inApps = new PluginYG2InAppProvider();
                    envir = new PluginYG2EnvirProvider();
                    player = new PluginYG2PlayerProvider(settings.PlayerStorage);
                    // leaderboards = new PluginYG2LeaderboardProvider();
#endif
                    break;

                case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEPUSH
                    // Лидерборды останутся пустышкой!
                    ads = new GamePushAdvProvider();
                    inApps = new GamePushInAppProvider();
                    envir = new GamePushEnvirProvider();
                    player = new GamePushPlayerProvider(settings.PlayerStorage);
#endif
                    break;
                case UniWebSettings.TargetPlatform.VKGames:
#if UNIWEB_VK_GAMES
                    player = new VKGamesPlayerProvider(settings.PlayerStorage);
#endif
                    break;
            }

            // 3. Отправляем в мост
            UniWebBridge.Initialize(ads, inApps, envir, player);
        }
    }
}
#endif
