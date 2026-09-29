using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3MovieDnaBuilder
{
    public const string DefaultIdentityTitle = "Explorer";

    public static InsightsV3MovieDnaResult Build(InsightsV3RawData raw, DateTime utcNow)
    {
        var labels = InsightsMovieDnaBuilder.BuildFromAggregates(
            raw.DistinctMovieCount,
            raw.DistinctSeriesCount,
            raw.AllTimeGenreContributions,
            raw.AllTimeTitlesWithGenres,
            raw.AllTimeReleaseYears,
            utcNow.Year);
        var genres = InsightsTasteBuilder.BuildGenres(
            raw.AllTimeGenreContributions,
            raw.AllTimeTitlesWithGenres);
        var mix = BuildWatchingMix(raw.DistinctMovieCount, raw.DistinctSeriesCount);

        return new InsightsV3MovieDnaResult(
            BuildIdentityTitle(labels),
            labels.Select(label => label.Code).ToList(),
            labels,
            genres,
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
