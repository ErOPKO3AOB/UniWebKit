#if UNIWEB_VK_GAMES
using System;

namespace UniWebKit
{
    public sealed class VKGamesProfileProvider : ProfileProviderBase
    {
        private VKGamesRuntime _runtime;
        private Action _cancel;
        private Action<UniWebError> _onError;
        private bool _busy;
        public override bool IsSupported => !IsDisposed && VKGamesRuntime.IsBrowser;
        public override bool IsBusy => _busy;

        public VKGamesProfileProvider()
        {
            if (!IsSupported) return;
            _runtime = VKGamesRuntime.Instance;
            _runtime.ProfileInvalidated += RefreshOnResume;
            Refresh();
        }

        private void RefreshOnResume() { if (!IsDisposed && !_busy) Refresh(); }

        public override void Refresh(Action<ProfileInfo> onSuccess = null, Action<UniWebError> onError = null)
        {
            if (IsDisposed || !IsSupported) { base.Refresh(onSuccess, onError); return; }
            if (_busy) { UniWebCallbacks.Invoke(() => onError?.Invoke(new UniWebError(UniWebErrorCode.Busy, "Profile refresh is pending."))); return; }
            _busy = true; _onError = onError;
            _cancel = VKGamesRuntime.Request("profile", null, reply =>
            {
                _busy = false; _cancel = null; _onError = null;
                if (!reply.success) { UniWebCallbacks.Invoke(() => onError?.Invoke(reply.Error)); return; }
                if (string.IsNullOrEmpty(reply.id))
                {
                    UniWebCallbacks.Invoke(() => onError?.Invoke(new UniWebError(UniWebErrorCode.InvalidData, "VK did not provide a player ID.")));
                    return;
                }
                var profile = new ProfileInfo(reply.id, reply.name, reply.avatar);
                SetProfile(profile);
                UniWebCallbacks.Invoke(() => onSuccess?.Invoke(profile));
            });
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            if (_runtime != null) _runtime.ProfileInvalidated -= RefreshOnResume;
            _cancel?.Invoke(); _cancel = null;
            var callback = _onError; _onError = null; _busy = false;
            base.Dispose();
            UniWebCallbacks.Invoke(() => callback?.Invoke(new UniWebError(UniWebErrorCode.Disposed, "Profile provider disposed.")));
        }
    }
}
#endif
