#if UNIWEB_ARCHITECTURE_STATIC

namespace UniWebKit
{
    public static class UniWebBridge
    {
        // For INIT
        public static void Initialize(IAdvProvider ads = null, IInAppProvider inApps = null, IEnvirProvider envir = null, IPlayerProvider player = null, ILocalizationProvider localization = null, ILeaderboardProvider leaderboards = null,
            IPlatformProvider platform = null, IProfileProvider profile = null, IGameProvider game = null, IPauseProvider pause = null)
        {
            if (!ReferenceEquals(Ads, ads)) (Ads as System.IDisposable)?.Dispose();
            if (!ReferenceEquals(InApps, inApps)) InApps?.Dispose();
            if (!ReferenceEquals(Player, player)) Player?.Dispose();
            if (!ReferenceEquals(Localization, localization)) Localization?.Dispose();
            if (!ReferenceEquals(Leaderboards, leaderboards)) Leaderboards?.Dispose();
            if (!ReferenceEquals(Platform, platform)) Platform?.Dispose();
            if (!ReferenceEquals(Profile, profile)) Profile?.Dispose();
            if (!ReferenceEquals(Game, game)) (Game as System.IDisposable)?.Dispose();
            if (!ReferenceEquals(Pause, pause)) Pause?.Dispose();
            
            Ads = ads ?? new UnsupportedAdvProvider();
            InApps = inApps ?? new UnsupportedInAppProvider();
            Envir = envir ?? new UnityEnvirProvider();
            Player = player ?? new LocalPrefsPlayerProvider();
            Localization = localization ?? new LocalLocalizationProvider();
            Leaderboards = leaderboards ?? new UnsupportedLeaderboardProvider();
            Platform = platform ?? new UnsupportedPlatformProvider();
            Profile = profile ?? new UnsupportedProfileProvider();
            Game = game ?? new UnsupportedGameProvider();
            Pause = pause ?? new LocalPauseProvider();
            
            IsInitialized = true;
        }
        public static bool IsInitialized { get; private set; }

        public static IAdvProvider Ads { get; private set; }
        public static IInAppProvider InApps { get; private set; } = new UnsupportedInAppProvider();
        public static IEnvirProvider Envir { get; private set; } = new UnityEnvirProvider();
        public static IPlayerProvider Player { get; private set; } = new LocalPrefsPlayerProvider();
        public static ILocalizationProvider Localization { get; private set; } = new LocalLocalizationProvider();
        public static ILeaderboardProvider Leaderboards { get; private set; } = new UnsupportedLeaderboardProvider();
        public static IPlatformProvider Platform { get; private set; } = new UnsupportedPlatformProvider();
        public static IProfileProvider Profile { get; private set; } = new UnsupportedProfileProvider();
        public static IGameProvider Game { get; private set; } = new UnsupportedGameProvider();
        public static IPauseProvider Pause { get; private set; } = new LocalPauseProvider();
    }
}
#endif
