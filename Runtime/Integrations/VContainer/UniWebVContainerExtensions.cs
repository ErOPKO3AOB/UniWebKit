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
            if (settings == null) return;

            // 1. СТЕЛЕМ СОЛОМКУ (Shadow Fallbacks)
            // Регистрируем "пустышки" для всех фичей плагина. 
            // Если платформа ниже их не перезапишет, игра получит эти безопасные заглушки с IsSupported = false.
            builder.Register<UnsupportedAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
            //builder.Register<UnsupportedLeaderboardProvider>(Lifetime.Singleton).As<ILeaderboardProvider>();
            // builder.Register<LocalPrefsPlayerProvider>(Lifetime.Singleton).As<IPlayerProvider>(); // Для сохранений локальный фолбэк

            // 2. ПЕРЕЗАПИСЫВАЕМ ТЕМ, ЧТО РЕАЛЬНО ЕСТЬ
            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.PluginYG2:
#if UNIWEB_PLUGIN_YG2
                    // Яндекс умеет всё. Он перезапишет ВСЕ дефолтные заглушки своими реализациями.
                    builder.Register<PluginYG2AdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
                    //builder.Register<PluginYG2LeaderboardProvider>(Lifetime.Singleton).As<ILeaderboardProvider>();
#endif
                    break;

                case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEPUSH
                    // GameDistribution имеет ТОЛЬКО рекламу. 
                    // Мы регистрируем только AdvProvider. 
                    // Лидерборды останутся от UnsupportedLeaderboardProvider! Никаких крашей.
                    builder.Register<GamePushAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
#endif
                    break;
            }
        }
    }
}
#endif