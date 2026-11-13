using System;
using System.Collections.Generic;

namespace UniWebKit
{
    public sealed class LeaderboardResult
    {
        public string LeaderboardId { get; }
        public IReadOnlyList<LeaderboardEntry> Entries { get; }

        public LeaderboardResult(string leaderboardId, IEnumerable<LeaderboardEntry> entries = null)
        {
            if (string.IsNullOrWhiteSpace(leaderboardId)) throw new ArgumentException("A leaderboard ID is required.", nameof(leaderboardId));
            var copy = new List<LeaderboardEntry>();
            if (entries != null)
                foreach (var entry in entries)
                {
                    if (entry == null) throw new ArgumentException("Entries must not contain null.", nameof(entries));
                    copy.Add(entry);
                }
            LeaderboardId = leaderboardId;
            Entries = copy.AsReadOnly();
        }
    }
}
