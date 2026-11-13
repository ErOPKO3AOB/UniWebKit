using System;

namespace UniWebKit
{
    public abstract class LeaderboardProviderBase : ILeaderboardProvider
    {
        protected bool IsDisposed { get; private set; }
        public virtual bool IsSupported => !IsDisposed;
        public virtual bool IsReady => false;
        public virtual bool IsBusy => false;

        public virtual void Fetch(string leaderboardId, Action<LeaderboardResult> onSuccess = null,
            Action<LeaderboardError> onError = null, int limit = 10)
            => ReportUnsupported(onError, leaderboardId);

        public virtual void FetchCurrentPlayerEntry(string leaderboardId, Action<LeaderboardEntry> onSuccess = null,
            Action<LeaderboardError> onError = null)
            => ReportUnsupported(onError, leaderboardId);

        public virtual void SetScore(string leaderboardId, decimal score, Action<LeaderboardSubmitResult> onSuccess = null,
            Action<LeaderboardError> onError = null, string extraData = null)
            => ReportUnsupported(onError, leaderboardId);

        protected void ReportUnsupported(Action<LeaderboardError> onError, string leaderboardId = null)
            => onError?.Invoke(new LeaderboardError(IsDisposed ? LeaderboardErrorCode.Disposed : LeaderboardErrorCode.Unsupported,
                IsDisposed ? "The leaderboards provider has been disposed." : "This leaderboard operation is not supported.", leaderboardId));

        public virtual void Dispose() => IsDisposed = true;
    }
}
