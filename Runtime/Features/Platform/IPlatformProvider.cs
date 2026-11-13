using System;

namespace UniWebKit
{
    public interface IPlatformProvider : IUniWebFeature, IDisposable
    {
        bool IsReady { get; }
        bool IsBusy { get; }
        event Action Initialized;
        void Initialize(Action onReady = null, Action<UniWebError> onError = null);
    }
}
