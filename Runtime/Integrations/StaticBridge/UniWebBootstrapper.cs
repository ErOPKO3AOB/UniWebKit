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
            //ILeaderboardProvider leaderboards = new UnsupportedLeaderboardProvider();
            // IPlayerProvider player = new LocalPrefsPlayerProvider();

            // 2. ПЕРЕЗАПИСЫВАЕМ (если платформа это умеет)
            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.PluginYG2:
#if UNIWEB_PLUGIN_YG2
                    ads = new PluginYG2AdvProvider();
                    // leaderboards = new PluginYG2LeaderboardProvider();
#endif
                    break;

                case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEDISTRIBUTION
                    // GD умеет только в рекламу. Перезаписываем только её.
                    // Лидерборды останутся пустышкой!
                    ads = new GamePushAdvProvider();
#endif
                    break;
            }

            // 3. Отправляем в мост
            UniWebBridge.Initialize(ads/*, leaderboards , player */);
        }
    }
}
#endif