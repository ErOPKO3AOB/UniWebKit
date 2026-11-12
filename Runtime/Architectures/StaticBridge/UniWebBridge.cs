#if UNIWEB_ARCHITECTURE_STATIC

namespace UniWebKit
{
    public static class UniWebBridge
    {
        // For INIT
        public static void Initialize(IAdvProvider ads = null, IInAppProvider inApps = null)
        {
            if (!ReferenceEquals(InApps, inApps)) InApps?.Dispose();
            Ads = ads != null ? ads : new UnsupportedAdvProvider();
            InApps = inApps ?? new UnsupportedInAppProvider();
            //Player = player;
            IsInitialized = true;
        }
        public static bool IsInitialized { get; private set; }

        public static IAdvProvider Ads { get; private set; }
        public static IInAppProvider InApps { get; private set; } = new UnsupportedInAppProvider();
        //public static IPlayerProvider Player { get; private set; }
    }
}
#endif
