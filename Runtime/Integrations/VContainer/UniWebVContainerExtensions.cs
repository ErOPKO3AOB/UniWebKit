#if UNIWEB_ARCHITECTURE_VCONTAINER
using VContainer;
using UnityEngine;

namespace UniWebKit
{
    public static class UniWebVContainerExtensions
    {
        public static void RegisterUniWeb(this IContainerBuilder builder)
        {
            // Загружаем настройки прямо из Resources
            var settings = Resources.Load<UniWebSettings>("UniWebSettings");

            if (settings == null)
            {
                Debug.LogError("[UniWeb API] Не удалось найти UniWebSettings в папке Resources! Провайдеры не зарегистрированы.");
                return;
            }

            switch (settings.CurrentPlatform)
            {
                case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEPUSH
                    builder.Register<GamePushAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
#else
                    Debug.LogError("[UniWeb API] Выбран GamePush, но дефайн UNIWEB_GAMEPUSH не активен!");
#endif
                    break;

                case UniWebSettings.TargetPlatform.PluginYG2:
#if UNIWEB_PLUGIN_YG2
                    builder.Register<PluginYG2AdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
#else
                    Debug.LogError("[UniWeb API] Выбран PluginYG2, но дефайн UNIWEB_PLUGIN_YG2 не активен!");
#endif
                    break;
            }
        }
    }
}
#endif