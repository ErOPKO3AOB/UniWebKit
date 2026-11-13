using System;

namespace UniWebKit
{
    public abstract class PlatformProviderBase : IPlatformProvider
    {
        protected bool IsDisposed { get; private set; }
        public virtual bool IsSupported => !IsDisposed;
        public bool IsReady { get; private set; }
        public virtual bool IsBusy => false;
        public event Action Initialized;
        public virtual void Initialize(Action onReady = null, Action<UniWebError> onError = null)
            => UniWebCallbacks.Invoke(() => onError?.Invoke(new UniWebError(
                IsDisposed ? UniWebErrorCode.Disposed : UniWebErrorCode.Unsupported, "Platform initialization is unavailable.")));
        protected void MarkInitialized()
        {
            if (IsDisposed || IsReady) return;
            IsReady = true;
            UniWebCallbacks.Raise(Initialized);
        }
        public virtual void Dispose() { IsDisposed = true; IsReady = false; Initialized = null; }
    }

    public sealed class UnsupportedPlatformProvider : PlatformProviderBase
    {
        public override bool IsSupported => false;
    }
}
