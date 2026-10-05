namespace MovieApp.Application.Services.WatchHistory;

/// <summary>
/// Next-episode and Continue Watching treat only aired episodes with a known air date as watchable.
/// Persistence queries mirror these rules in SQL; keep them in sync.
/// </summary>
public static class EpisodeWatchEligibility
{
    public static DateOnly TodayUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

    public static bool IsCurrentlyWatchable(DateOnly? airDate, DateOnly today) =>
        airDate is not null && airDate.Value <= today;
}
