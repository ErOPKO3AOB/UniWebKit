using System;

namespace UniWebKit
{
    public sealed class LeaderboardSubmitResult
    {
        public string LeaderboardId { get; }
        // The submitted value, not a claim about the player's resulting best score or rank.
        public decimal SubmittedScore { get; }
        public bool IsConfirmed { get; }

        public LeaderboardSubmitResult(string leaderboardId, decimal submittedScore, bool isConfirmed)
        {
            if (string.IsNullOrWhiteSpace(leaderboardId)) throw new ArgumentException("A leaderboard ID is required.", nameof(leaderboardId));
            LeaderboardId = leaderboardId;
            SubmittedScore = submittedScore;
            IsConfirmed = isConfirmed;
        }
    }
}
