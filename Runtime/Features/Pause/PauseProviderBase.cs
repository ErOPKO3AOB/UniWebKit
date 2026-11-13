using System;

namespace UniWebKit
{
    public abstract class PauseProviderBase : IPauseProvider
    {
        private bool _manual, _platform;
        protected bool IsDisposed { get; private set; }
        public virtual bool IsSupported => !IsDisposed;
        public bool IsPaused { get; private set; }
        public event Action<bool> PauseChanged;
        public virtual void SetPaused(bool paused) { if (IsDisposed) return; _manual = paused; UpdatePause(); }
        protected void SetPlatformPaused(bool paused) { if (IsDisposed) return; _platform = paused; UpdatePause(); }
        private void UpdatePause()
        {
            bool paused = _manual || _platform;
            if (paused == IsPaused) return;
            IsPaused = paused;
            UniWebCallbacks.Raise(PauseChanged, paused);
        }
        // Consumers own Time.timeScale and audio; the provider only reports combined state.
        public virtual void Dispose() { IsDisposed = true; IsPaused = false; PauseChanged = null; }
    }

    public sealed class LocalPauseProvider : PauseProviderBase { }

    public sealed class UnsupportedPauseProvider : PauseProviderBase
    {
        public override bool IsSupported => false;
        public override void SetPaused(bool paused) { }
    }
}
