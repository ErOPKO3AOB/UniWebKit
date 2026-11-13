using System;

namespace UniWebKit
{
    public interface IProfileProvider : IUniWebFeature, IDisposable
    {
        bool IsReady { get; }
        bool IsBusy { get; }
        ProfileInfo CurrentProfile { get; }
        // Initial profile and changes to its identity or display data.
        event Action<ProfileInfo> ProfileChanged;
        void Refresh(Action<ProfileInfo> onSuccess = null, Action<UniWebError> onError = null);
    }
}
