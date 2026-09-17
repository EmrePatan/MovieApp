using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsEstimatedTimeBuilder
{
    public static InsightsEstimatedTimeWatchedResult Build(
        InsightsAnalyticsRawData raw,
        TimeZoneInfo timeZone,
        DateTime utcNow)
    {
        var totalWatchedItems = raw.MoviesWatched + raw.EpisodesWatched;
        var knownRuntimeItemCount = raw.MoviesWithKnownRuntime + raw.EpisodesWithKnownRuntime;

        if (knownRuntimeItemCount == 0 || totalWatchedItems == 0)
        {
            return new InsightsEstimatedTimeWatchedResult(
                0,
                0,
                0,
                0,
                totalWatchedItems,
                0m,
                null);
        }

        var movieMinutes = raw.MovieEstimatedMinutes;
        var episodeMinutes = raw.EpisodeEstimatedMinutes;
        var totalMinutes = movieMinutes + episodeMinutes;
        var coveragePercent = Math.Round(
            knownRuntimeItemCount * 100m / totalWatchedItems,
            1,
            MidpointRounding.AwayFromZero);

        var currentYear = ToLocalDate(utcNow, timeZone).Year;
        var currentYearMinutes = raw.ActivityEvents
            .Where(eventData =>
                eventData.EstimatedMinutes is > 0 &&
                ToLocalDate(eventData.WatchedAtUtc, timeZone).Year == currentYear)
            .Sum(eventData => eventData.EstimatedMinutes ?? 0);

        return new InsightsEstimatedTimeWatchedResult(
            totalMinutes,
            movieMinutes,
            episodeMinutes,
            knownRuntimeItemCount,
            totalWatchedItems,
            coveragePercent,
            currentYearMinutes > 0 ? currentYearMinutes : null);
    }

    private static DateOnly ToLocalDate(DateTime timestampUtc, TimeZoneInfo timeZone)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc
            : DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }
}
