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
        var uniqueTitles = raw.MoviesWatched + raw.ShowsStarted;
        if (uniqueTitles < MinimumUniqueTitles)
        {
            return [];
        }

        var labels = new List<InsightsMovieDnaLabelResult>(3);

        var topGenre = TryBuildTopGenreLabel(raw);
        if (topGenre is not null)
        {
            labels.Add(topGenre);
        }

        var formatLabel = TryBuildFormatLabel(raw.MoviesWatched, raw.ShowsStarted, uniqueTitles);
        if (formatLabel is not null)
        {
            labels.Add(formatLabel);
        }

        var recentReleasesLabel = TryBuildRecentReleasesLabel(raw, utcNow.Year);
        if (recentReleasesLabel is not null)
        {
            labels.Add(recentReleasesLabel);
        }

        return labels.Take(3).ToList();
    }

    private static InsightsMovieDnaLabelResult? TryBuildTopGenreLabel(InsightsSummaryRawData raw)
    {
        var titlesWithGenres = raw.MovieTitles
            .Concat(raw.TvShowTitles)
            .Where(title => title.Genres.Count > 0)
            .ToList();

        if (titlesWithGenres.Count < MinimumGenreTitles)
        {
            return null;
        }

        var genreWeights = new Dictionary<Guid, (string Name, decimal Weight)>();

        foreach (var title in titlesWithGenres)
        {
            var weightPerGenre = 1m / title.Genres.Count;
            foreach (var genre in title.Genres)
            {
                if (genreWeights.TryGetValue(genre.GenreId, out var existing))
                {
                    genreWeights[genre.GenreId] = (existing.Name, existing.Weight + weightPerGenre);
                    continue;
                }

                genreWeights[genre.GenreId] = (genre.Name, weightPerGenre);
            }
        }

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
        InsightsSummaryRawData raw,
        int currentYear)
    {
        var titlesWithKnownYear = raw.MovieTitles
            .Concat(raw.TvShowTitles)
            .Where(title => title.ReleaseYear.HasValue)
            .ToList();

        if (titlesWithKnownYear.Count < MinimumRecentReleaseTitles)
        {
            return null;
        }

        var minimumYear = currentYear - RecentReleaseYearWindow;
        var recentCount = titlesWithKnownYear.Count(title => title.ReleaseYear >= minimumYear);
        var recentShare = recentCount / (decimal)titlesWithKnownYear.Count;

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
