namespace UniWebKit
{
    public sealed class LeaderboardError
    {
        public LeaderboardErrorCode Code { get; }
        public string Message { get; }
        public string LeaderboardId { get; }

        public LeaderboardError(LeaderboardErrorCode code, string message, string leaderboardId = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            LeaderboardId = leaderboardId ?? string.Empty;
        }

        public override string ToString() => $"{Code}: {Message}";
    }
}
