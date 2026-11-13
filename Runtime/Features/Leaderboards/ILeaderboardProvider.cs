using System;

namespace UniWebKit
{
    public interface ILeaderboardProvider : IUniWebFeature, IDisposable
    {
        bool IsReady { get; }
        bool IsBusy { get; }

        // Callbacks may run synchronously. Success returns a snapshot for this request.
        void Fetch(string leaderboardId, Action<LeaderboardResult> onSuccess = null,
            Action<LeaderboardError> onError = null, int limit = 10);

        // Success with null means that the current player has no entry, not a request failure.
        void FetchCurrentPlayerEntry(string leaderboardId, Action<LeaderboardEntry> onSuccess = null,
            Action<LeaderboardError> onError = null);

        // Dispatch and confirmed persistence are distinct; inspect LeaderboardSubmitResult.IsConfirmed.
        void SetScore(string leaderboardId, decimal score, Action<LeaderboardSubmitResult> onSuccess = null,
            Action<LeaderboardError> onError = null, string extraData = null);
    }
}
