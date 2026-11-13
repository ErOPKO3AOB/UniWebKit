#if UNIWEB_ARCHITECTURE_STATIC

namespace UniWebKit
{
    public static class UniWebBridge
    {
        // For INIT
        public static void Initialize(IAdvProvider ads = null, IInAppProvider inApps = null, IEnvirProvider envir = null, IPlayerProvider player = null, ILocalizationProvider localization = null, ILeaderboardProvider leaderboards = null)
        {
            if (!ReferenceEquals(InApps, inApps)) InApps?.Dispose();
            if (!ReferenceEquals(Player, player)) Player?.Dispose();
            if (!ReferenceEquals(Localization, localization)) Localization?.Dispose();
            if (!ReferenceEquals(Leaderboards, leaderboards)) Leaderboards?.Dispose();
            
            Ads = ads ?? new UnsupportedAdvProvider();
            InApps = inApps ?? new UnsupportedInAppProvider();
            Envir = envir ?? new UnityEnvirProvider();
            Player = player ?? new LocalPrefsPlayerProvider();
            Localization = localization ?? new LocalLocalizationProvider();
            Leaderboards = leaderboards ?? new UnsupportedLeaderboardProvider();
            
            IsInitialized = true;
        }
        public static bool IsInitialized { get; private set; }

        public static IAdvProvider Ads { get; private set; }
        public static IInAppProvider InApps { get; private set; } = new UnsupportedInAppProvider();
        public static IEnvirProvider Envir { get; private set; } = new UnityEnvirProvider();
        public static IPlayerProvider Player { get; private set; } = new LocalPrefsPlayerProvider();
        public static ILocalizationProvider Localization { get; private set; } = new LocalLocalizationProvider();
        public static ILeaderboardProvider Leaderboards { get; private set; } = new UnsupportedLeaderboardProvider();
    }
}
#endif
