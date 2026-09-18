using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3TimeInStoriesBuilder
{
    public static InsightsV3TimeInStoriesResult Build(InsightsV3RawData raw)
    {
        var totalWatchedItems = raw.MoviesWatched + raw.EpisodesWatched;
        var knownRuntimeItemCount = raw.MoviesWithKnownRuntime + raw.EpisodesWithKnownRuntime;
        var totalMinutes = raw.MovieEstimatedMinutes + raw.EpisodeEstimatedMinutes;

        var coveragePercent = totalWatchedItems == 0 || knownRuntimeItemCount == 0
            ? 0m
            : Math.Round(
                knownRuntimeItemCount * 100m / totalWatchedItems,
                1,
                MidpointRounding.AwayFromZero);

        var yearMinutes = raw.YearMovieWatches
            .Where(watch => watch.RuntimeMinutes is > 0)
            .Sum(watch => watch.RuntimeMinutes ?? 0)
            + raw.YearEpisodeWatches
                .Where(watch => watch.RuntimeMinutes is > 0)
                .Sum(watch => watch.RuntimeMinutes ?? 0);

        return new InsightsV3TimeInStoriesResult(
            totalMinutes,
            raw.MovieEstimatedMinutes,
            raw.EpisodeEstimatedMinutes,
            yearMinutes,
            coveragePercent);
    }
}
