#if UNIWEB_ARCHITECTURE_STATIC

namespace UniWebKit
{
    public static class UniWebBridge
    {
        public static void Initialize(IAdvProvider ads, IPlayerProvider player)
        {
            Ads = ads;
            Player = player;
            IsInitialized = true;
        }
        public static bool IsInitialized { get; private set; }

        public static IAdvProvider Ads { get; private set; }
        public static IPlayerProvider Player { get; private set; }
    }
}
#endif