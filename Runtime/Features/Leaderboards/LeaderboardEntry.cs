using System;

namespace UniWebKit
{
    public sealed class LeaderboardEntry
    {
        // Opaque ID. Empty when the platform does not disclose it.
        public string PlayerId { get; }
        public string DisplayName { get; }
        public string AvatarUrl { get; }
        public decimal Score { get; }
        // One-based position; null means unknown. Never substitute a local array index.
        public long? Rank { get; }
        public bool IsCurrentPlayer { get; }
        public string ExtraData { get; }
        public string FormattedScore { get; }

        public LeaderboardEntry(decimal score, string playerId = null, string displayName = null,
            string avatarUrl = null, long? rank = null, bool isCurrentPlayer = false,
            string extraData = null, string formattedScore = null)
        {
            if (rank.HasValue && rank.Value < 1) throw new ArgumentOutOfRangeException(nameof(rank));
            Score = score;
            PlayerId = playerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            AvatarUrl = avatarUrl ?? string.Empty;
            Rank = rank;
            IsCurrentPlayer = isCurrentPlayer;
            ExtraData = extraData;
            FormattedScore = formattedScore;
        }
    }
}
