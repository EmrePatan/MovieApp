namespace MovieApp.Api.BackgroundJobs;

public static class RecurringJobIds
{
    public const string TmdbTvChanges = "movieapp:tmdb-tv-changes";
    public const string HotRelease = "movieapp:hot-release";
    public const string MovieRelease = "movieapp:movie-release";
    public const string ReleaseFanout = "movieapp:release-fanout";
    public const string PushPreparation = "movieapp:push-preparation";
    public const string PushDispatch = "movieapp:push-dispatch";
    public const string PushReceipts = "movieapp:push-receipts";

    public static IReadOnlyList<string> All =>
    [
        TmdbTvChanges,
        HotRelease,
        MovieRelease,
        ReleaseFanout,
        PushPreparation,
        PushDispatch,
        PushReceipts
    ];
}
