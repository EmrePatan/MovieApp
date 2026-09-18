using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3MovieDnaBuilder
{
    public const string DefaultIdentityTitle = "Explorer";

    public static InsightsV3MovieDnaResult Build(InsightsV3RawData raw, DateTime utcNow)
    {
        var summaryRaw = new InsightsSummaryRawData(
            raw.MemberSinceUtc,
            raw.DistinctMovieCount,
            raw.EpisodesWatched,
            raw.DistinctSeriesCount,
            raw.RatingsCount,
            raw.RatingScoreCounts,
            raw.MovieTitles,
            raw.TvShowTitles);

        var labels = InsightsMovieDnaBuilder.Build(summaryRaw, utcNow);
        var taste = InsightsTasteBuilder.Build(raw.MilestoneRaw);
        var mix = BuildWatchingMix(raw.DistinctMovieCount, raw.DistinctSeriesCount);

        return new InsightsV3MovieDnaResult(
            BuildIdentityTitle(labels),
            labels.Select(label => label.Code).ToList(),
            labels,
            taste.Genres,
            mix);
    }

    public static string BuildIdentityTitle(IReadOnlyList<InsightsMovieDnaLabelResult> labels)
    {
        if (labels.Count == 0)
        {
            return DefaultIdentityTitle;
        }

        var topGenre = labels.FirstOrDefault(label => label.Code == InsightsMovieDnaBuilder.TopGenreCode);
        var formatLabel = labels.FirstOrDefault(label => label.Category == InsightsMovieDnaBuilder.FormatCategory);
        var eraLabel = labels.FirstOrDefault(label => label.Category == InsightsMovieDnaBuilder.EraCategory);

        if (topGenre is not null && formatLabel is not null)
        {
            return $"{topGenre.Label} {formatLabel.Label}";
        }

        if (topGenre is not null)
        {
            return topGenre.Label;
        }

        if (formatLabel is not null)
        {
            return formatLabel.Label;
        }

        return eraLabel?.Label ?? DefaultIdentityTitle;
    }

    public static InsightsV3WatchingMixResult BuildWatchingMix(int distinctMovieCount, int distinctSeriesCount)
    {
        var total = distinctMovieCount + distinctSeriesCount;
        if (total == 0)
        {
            return new InsightsV3WatchingMixResult(0, 0, 0m, 0m);
        }

        var movieShare = Math.Round(distinctMovieCount * 100m / total, 1, MidpointRounding.AwayFromZero);
        var seriesShare = Math.Round(100m - movieShare, 1, MidpointRounding.AwayFromZero);

        return new InsightsV3WatchingMixResult(
            distinctMovieCount,
            distinctSeriesCount,
            movieShare,
            seriesShare);
    }
}
