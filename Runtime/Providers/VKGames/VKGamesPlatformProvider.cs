#if UNIWEB_VK_GAMES
using System;
using System.Collections.Generic;

namespace UniWebKit
{
    public sealed class VKGamesPlatformProvider : PlatformProviderBase
    {
        private sealed class Waiter { internal Action ready; internal Action<UniWebError> error; }
        private readonly List<Waiter> _waiters = new List<Waiter>();
        private Action _cancel;
        private bool _busy;
        public override bool IsSupported => !IsDisposed && VKGamesRuntime.IsBrowser;
        public override bool IsBusy => _busy;

        public VKGamesPlatformProvider() { Initialize(); }

        public override void Initialize(Action onReady = null, Action<UniWebError> onError = null)
        {
            if (IsDisposed || !IsSupported) { base.Initialize(onReady, onError); return; }
            if (IsReady) { UniWebCallbacks.Invoke(onReady); return; }
            _waiters.Add(new Waiter { ready = onReady, error = onError });
            if (_busy) return;
            _busy = true;
            _cancel = VKGamesRuntime.Request("init", null, reply =>
            {
                _cancel = null;
                _busy = false;
                var waiters = _waiters.ToArray();
                _waiters.Clear();
                if (reply.success) MarkInitialized();
                foreach (var waiter in waiters)
                    if (reply.success) UniWebCallbacks.Invoke(waiter.ready);
                    else UniWebCallbacks.Invoke(() => waiter.error?.Invoke(reply.Error));
            });
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            _cancel?.Invoke(); _cancel = null; _busy = false;
            var waiters = _waiters.ToArray(); _waiters.Clear();
            base.Dispose();
            foreach (var waiter in waiters)
                UniWebCallbacks.Invoke(() => waiter.error?.Invoke(new UniWebError(UniWebErrorCode.Disposed, "Platform provider disposed.")));
        }
    }
}
#endif
