#if UNIWEB_ARCHITECTURE_STATIC
using UnityEngine;

namespace UniWebKit
{
    public static class UniWebBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            var settings = Resources.Load<UniWebSettings>("UniWebSettings");
            if (settings == null || !settings.AutoInitializeStaticAPI)
                return;

            IAdvProvider ads = null;
            IPlayerProvider player = null;

            //switch (settings.CurrentPlatform)
            //{
            //case UniWebSettings.TargetPlatform.GamePush:
#if UNIWEB_GAMEPUSH
            ads = new GamePushAdvProvider();
                    // player = new GamePushPlayerProvider();
                    //break;
                //case UniWebSettings.TargetPlatform.Mock:
#elif UNIWEB_PLUGIN_YG2
            ads = new PluginYG2AdvProvider(); // Заглушка для тестов в редакторе
                                              //break;
#endif
            //}

            UniWebBridge.Initialize(ads, player);
        }
    }
}
#endif