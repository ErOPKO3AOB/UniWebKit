using System;
using System.Collections.Generic;
using UniWebKit;

static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }

    private static void CheckUnsupported(ILeaderboardProvider provider, LeaderboardErrorCode expected)
    {
        var requests = new Action<Action, Action<LeaderboardError>>[]
        {
            (success, error) => provider.Fetch("season-1", _ => success(), error),
            (success, error) => provider.FetchCurrentPlayerEntry("season-1", _ => success(), error),
            (success, error) => provider.SetScore("season-1", 100, _ => success(), error)
        };
        foreach (var request in requests)
        {
            int successes = 0, failures = 0;
            request(() => successes++, error =>
            {
                failures++;
                Check(error.Code == expected && error.LeaderboardId == "season-1", "error code and request identity");
            });
            Check(successes == 0 && failures == 1, "exactly one error and no false success");
        }
        provider.Fetch("season-1");
        provider.FetchCurrentPlayerEntry("season-1");
        provider.SetScore("season-1", 100);
        Check(!provider.IsReady && !provider.IsBusy, "no pending operation in fallback");
    }

    static void Main()
    {
        var unsupported = new UnsupportedLeaderboardProvider();
        Check(!unsupported.IsSupported, "unsupported feature can hide UI");
        CheckUnsupported(unsupported, LeaderboardErrorCode.Unsupported);
        unsupported.Dispose();
        unsupported.Dispose();
        CheckUnsupported(unsupported, LeaderboardErrorCode.Disposed);

        // Exact numbers and immutable snapshots are part of the vendor-independent contract.
        const decimal score = 9007199254740993m;
        var entry = new LeaderboardEntry(score, "opaque-id", rank: 42, isCurrentPlayer: true);
        var source = new List<LeaderboardEntry> { entry };
        var result = new LeaderboardResult("season-1", source);
        source.Clear();
        Check(result.Entries.Count == 1 && result.Entries[0].Score == score, "snapshot and integer precision");
        Check(result.Entries[0].Rank == 42 && result.Entries[0].IsCurrentPlayer, "preserve platform position");
        bool readOnly = false;
        try { ((IList<LeaderboardEntry>)result.Entries).Clear(); }
        catch (NotSupportedException) { readOnly = true; }
        Check(readOnly && result.Entries.Count == 1, "consumer cannot mutate snapshot");
        var anonymous = new LeaderboardEntry(12.5m);
        Check(anonymous.Rank == null && anonymous.PlayerId == "" && anonymous.AvatarUrl == "", "missing identity is not fabricated");
        Check(anonymous.Score == 12.5m && anonymous.FormattedScore == null, "fractional score and optional formatting");
        Check(new LeaderboardResult("empty").Entries.Count == 0, "empty top is representable");
        bool rejected = false;
        try { new LeaderboardEntry(0, rank: 0); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "rank zero is not a ranked entry");
        var dispatched = new LeaderboardSubmitResult("season-1", 100, false);
        var confirmed = new LeaderboardSubmitResult("season-1", 100, true);
        Check(!dispatched.IsConfirmed && confirmed.IsConfirmed && confirmed.SubmittedScore == 100, "dispatch differs from confirmation");

        Check(!UniWebBridge.Leaderboards.IsSupported, "bridge safe before initialization");
        var first = new TrackingFeature();
        UniWebBridge.Initialize(leaderboards: first);
        UniWebBridge.Initialize(leaderboards: first);
        Check(first.Disposals == 0 && ReferenceEquals(first, UniWebBridge.Leaderboards), "same instance survives reinitialization");
        var second = new TrackingFeature();
        UniWebBridge.Initialize(leaderboards: second);
        Check(first.Disposals == 1, "replacement disposes old feature");
        UniWebBridge.Initialize();
        Check(second.Disposals == 1 && !UniWebBridge.Leaderboards.IsSupported, "old Initialize call restores fallback");
        CheckUnsupported(UniWebBridge.Leaderboards, LeaderboardErrorCode.Unsupported);
        Console.WriteLine($"PASS: {checks} leaderboard checks");
    }

    private sealed class TrackingFeature : LeaderboardProviderBase
    {
        public int Disposals { get; private set; }
        public override void Dispose() { Disposals++; base.Dispose(); }
    }
}
