using System;

namespace UniWebKit
{
    public abstract class ProfileProviderBase : IProfileProvider
    {
        protected bool IsDisposed { get; private set; }
        public virtual bool IsSupported => !IsDisposed;
        public bool IsReady => !IsDisposed && CurrentProfile != null;
        public virtual bool IsBusy => false;
        public ProfileInfo CurrentProfile { get; private set; }
        public event Action<ProfileInfo> ProfileChanged;
        public virtual void Refresh(Action<ProfileInfo> onSuccess = null, Action<UniWebError> onError = null)
            => UniWebCallbacks.Invoke(() => onError?.Invoke(new UniWebError(
                IsDisposed ? UniWebErrorCode.Disposed : UniWebErrorCode.Unsupported, "Profile refresh is unavailable.")));
        protected void SetProfile(ProfileInfo profile)
        {
            if (IsDisposed) return;
            var previous = CurrentProfile;
            CurrentProfile = profile;
            if (ReferenceEquals(previous, profile)) return;
            if (previous != null && profile != null && previous.Id == profile.Id &&
                previous.Name == profile.Name && previous.AvatarUrl == profile.AvatarUrl) return;
            UniWebCallbacks.Raise(ProfileChanged, profile);
        }
        public virtual void Dispose() { IsDisposed = true; CurrentProfile = null; ProfileChanged = null; }
    }

    public sealed class UnsupportedProfileProvider : ProfileProviderBase
    {
        public override bool IsSupported => false;
    }
}
