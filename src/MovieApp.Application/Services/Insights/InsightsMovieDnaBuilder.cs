using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsMovieDnaBuilder
{
    public const string TopGenreCode = "top_genre";
    public const string SeriesFirstCode = "series_first";
    public const string MovieFirstCode = "movie_first";
    public const string RecentReleasesCode = "recent_releases";

    public const string GenreCategory = "genre";
    public const string FormatCategory = "format";
    public const string EraCategory = "era";

    private const decimal TopGenreShareThreshold = 0.25m;
    private const decimal SeriesFirstShareThreshold = 0.65m;
    private const decimal MovieFirstShareThreshold = 0.35m;
    private const decimal RecentReleaseShareThreshold = 0.50m;
    private const int MinimumUniqueTitles = 5;
    private const int MinimumGenreTitles = 5;
    private const int MinimumFormatTitles = 4;
    private const int MinimumRecentReleaseTitles = 5;
    private const int RecentReleaseYearWindow = 2;

    public static IReadOnlyList<InsightsMovieDnaLabelResult> Build(
        InsightsSummaryRawData raw,
        DateTime utcNow)
    {
        var titles = raw.MovieTitles.Concat(raw.TvShowTitles).ToList();
        var titlesWithGenres = titles
            .Where(title => title.Genres.Count > 0)
            .ToList();
        var releaseYears = titles
            .GroupBy(title => title.ReleaseYear)
            .Select(group => new InsightsV3ReleaseYearCount(group.Key, group.Count()))
            .ToList();

        return BuildFromAggregates(
            raw.MoviesWatched,
            raw.ShowsStarted,
            InsightsGenreContributions.FromTitles(titlesWithGenres),
            titlesWithGenres.Count,
            releaseYears,
            utcNow.Year);
    }

    public static IReadOnlyList<InsightsMovieDnaLabelResult> BuildFromAggregates(
        int moviesWatched,
        int showsStarted,
        IReadOnlyList<InsightsV3GenreContribution> genreContributions,
        int titlesWithGenres,
        IReadOnlyList<InsightsV3ReleaseYearCount> releaseYears,
        int currentYear)
    {
        var uniqueTitles = moviesWatched + showsStarted;
        if (uniqueTitles < MinimumUniqueTitles)
        {
            return [];
        }

        var labels = new List<InsightsMovieDnaLabelResult>(3);

        var topGenre = TryBuildTopGenreLabel(genreContributions, titlesWithGenres);
        if (topGenre is not null)
        {
            labels.Add(topGenre);
        }

        var formatLabel = TryBuildFormatLabel(moviesWatched, showsStarted, uniqueTitles);
        if (formatLabel is not null)
        {
            labels.Add(formatLabel);
        }

        var recentReleasesLabel = TryBuildRecentReleasesLabel(releaseYears, currentYear);
        if (recentReleasesLabel is not null)
        {
            labels.Add(recentReleasesLabel);
        }

        return labels.Take(3).ToList();
    }

    private static InsightsMovieDnaLabelResult? TryBuildTopGenreLabel(
        IReadOnlyList<InsightsV3GenreContribution> genreContributions,
        int titlesWithGenres)
    {
        if (titlesWithGenres < MinimumGenreTitles)
        {
            return null;
        }

        var genreWeights = InsightsGenreContributions.SumWeightsByGenreId(genreContributions);
        var totalWeight = genreWeights.Values.Sum(item => item.Weight);
        if (totalWeight <= 0)
        {
            return null;
        }

        var topGenre = genreWeights
            .Select(pair => new
            {
                pair.Key,
                pair.Value.Name,
                pair.Value.Weight,
                Share = pair.Value.Weight / totalWeight,
            })
            .OrderByDescending(item => item.Share)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Key)
            .First();

        if (topGenre.Share < TopGenreShareThreshold)
        {
            return null;
        }

        return new InsightsMovieDnaLabelResult(
            TopGenreCode,
            GenreCategory,
            topGenre.Name);
    }

    private static InsightsMovieDnaLabelResult? TryBuildFormatLabel(
        int moviesWatched,
        int showsStarted,
        int uniqueTitles)
    {
        if (uniqueTitles < MinimumFormatTitles)
        {
            return null;
        }

        var seriesShare = showsStarted / (decimal)uniqueTitles;
        if (seriesShare >= SeriesFirstShareThreshold)
        {
            return new InsightsMovieDnaLabelResult(
                SeriesFirstCode,
                FormatCategory,
                "Series-first");
        }

        if (seriesShare <= MovieFirstShareThreshold)
        {
            return new InsightsMovieDnaLabelResult(
                MovieFirstCode,
                FormatCategory,
                "Movie-first");
        }

        return null;
    }

    private static InsightsMovieDnaLabelResult? TryBuildRecentReleasesLabel(
        IReadOnlyList<InsightsV3ReleaseYearCount> releaseYears,
        int currentYear)
    {
        var titlesWithKnownYear = releaseYears
            .Where(row => row.Year.HasValue)
            .Sum(row => row.Count);

        if (titlesWithKnownYear < MinimumRecentReleaseTitles)
        {
            return null;
        }

        var minimumYear = currentYear - RecentReleaseYearWindow;
        var recentCount = releaseYears
            .Where(row => row.Year >= minimumYear)
            .Sum(row => row.Count);
        var recentShare = recentCount / (decimal)titlesWithKnownYear;

        if (recentShare < RecentReleaseShareThreshold)
        {
            return null;
        }

        return new InsightsMovieDnaLabelResult(
            RecentReleasesCode,
            EraCategory,
            "Recent releases");
    }
}
