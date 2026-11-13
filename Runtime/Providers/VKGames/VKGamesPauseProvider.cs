#if UNIWEB_VK_GAMES
namespace UniWebKit
{
    public sealed class VKGamesPauseProvider : PauseProviderBase
    {
        private readonly VKGamesRuntime _runtime;
        private bool _registered;
        public override bool IsSupported => !IsDisposed && VKGamesRuntime.IsBrowser;
        public VKGamesPauseProvider()
        {
            if (!IsSupported) return;
            VKGamesAdvProvider.PauseManagers++;
            _registered = true;
            _runtime = VKGamesRuntime.Instance;
            _runtime.PauseChanged += SetPlatformPaused;
            SetPlatformPaused(_runtime.IsPaused);
        }
        public override void Dispose()
        {
            if (_registered) { VKGamesAdvProvider.PauseManagers--; _registered = false; }
            if (_runtime != null) _runtime.PauseChanged -= SetPlatformPaused;
            base.Dispose();
        }
    }
}
#endif
