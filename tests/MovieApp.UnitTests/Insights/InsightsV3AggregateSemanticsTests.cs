using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsV3AggregateSemanticsTests
{
    private static readonly DateTime UtcNow = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GroupedContributionsMatchTitleListTasteDnaAndEras()
    {
        var action = Genre("Action");
        var drama = Genre("Drama");
        var comedy = Genre("Comedy");
        var sciFi = Genre("Sci-Fi");
        var movieTitles = new List<InsightsDnaTitleData>
        {
            Title(2024, [action, drama]),
            Title(2015, [action]),
            Title(null, []),
            Title(1995, [comedy]),
            Title(2026, [sciFi]),
            Title(1988, [drama]),
        };
        var tvTitles = new List<InsightsDnaTitleData>
        {
            Title(2018, [drama, sciFi]),
            Title(null, []),
        };
        var eligible = movieTitles.Concat(tvTitles).Where(title => title.Genres.Count > 0).ToList();
        var contributions = InsightsGenreContributions.FromTitles(eligible);
        var releaseYears = movieTitles.Concat(tvTitles)
            .GroupBy(title => title.ReleaseYear)
            .Select(group => new InsightsV3ReleaseYearCount(group.Key, group.Count()))
            .ToList();

        var tasteFromTitles = InsightsTasteBuilder.Build(AnalyticsRaw(movieTitles, tvTitles));
        var tasteFromGroups = InsightsTasteBuilder.BuildGenres(contributions, eligible.Count);
        Assert.Equal(tasteFromTitles.Genres, tasteFromGroups);

        var dnaFromTitles = InsightsMovieDnaBuilder.Build(
            SummaryRaw(movieTitles, tvTitles, moviesWatched: 6, showsStarted: 2),
            UtcNow);
        var dnaFromGroups = InsightsMovieDnaBuilder.BuildFromAggregates(
            6,
            2,
            contributions,
            eligible.Count,
            releaseYears,
            UtcNow.Year);
        Assert.Equal(dnaFromTitles, dnaFromGroups);

        var erasFromTitles = InsightsErasBuilder.Build(AnalyticsRaw(movieTitles, tvTitles));
        var erasFromGroups = InsightsErasBuilder.BuildFromYearCounts(releaseYears);
        Assert.Equal(erasFromTitles.UnknownCount, erasFromGroups.UnknownCount);
        Assert.Equal(
            erasFromTitles.Buckets.Select(bucket => (bucket.Bucket, bucket.Count, bucket.Percent)),
            erasFromGroups.Buckets.Select(bucket => (bucket.Bucket, bucket.Count, bucket.Percent)));

        Assert.Equal(4, InsightsMilestonesBuilder.CountDistinctGenres(AnalyticsRaw(movieTitles, tvTitles)));
        Assert.Equal(
            4,
            contributions.Select(contribution => contribution.GenreId).Distinct().Count());
    }

    [Fact]
    public void YearSplitKeepsDistinctShowsForAllTimeAndPerEpisodeForRisingGenre()
    {
        var drama = Genre("Drama");
        var sciFi = Genre("Sci-Fi");
        var show = Title(2018, [drama, sciFi]);
        var previousMovies = new List<InsightsDnaTitleData> { Title(2015, [drama]) };
        var currentEpisodes = new List<InsightsDnaTitleData> { show, show };

        var fromTitles = InsightsV3TasteBuilder.TryBuildRisingGenre(
            [],
            currentEpisodes,
            previousMovies,
            [show]);
        var fromGroups = InsightsV3TasteBuilder.TryBuildRisingGenre(
            InsightsGenreContributions.FromTitles(currentEpisodes),
            2,
            InsightsGenreContributions.FromTitles(previousMovies.Concat([show])),
            2);

        Assert.Equal(fromTitles, fromGroups);
        Assert.Equal(
            1,
            InsightsGenreContributions.FromTitles([show]).Single(row => row.GenreId == drama.GenreId).TitleCount);
        Assert.Equal(
            2,
            InsightsGenreContributions.FromTitles(currentEpisodes).Single(row => row.GenreId == drama.GenreId).TitleCount);
    }

    private static InsightsDnaGenreData Genre(string name) => new(Guid.NewGuid(), name);

    private static InsightsDnaTitleData Title(int? year, IReadOnlyList<InsightsDnaGenreData> genres) =>
        new(year, genres);

    private static InsightsAnalyticsRawData AnalyticsRaw(
        List<InsightsDnaTitleData> movies,
        List<InsightsDnaTitleData> shows) =>
        new(
            UtcNow,
            movies.Count,
            0,
            shows.Count,
            0,
            [],
            movies,
            shows,
            0,
            0,
            0,
            0,
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private static InsightsSummaryRawData SummaryRaw(
        List<InsightsDnaTitleData> movies,
        List<InsightsDnaTitleData> shows,
        int moviesWatched,
        int showsStarted) =>
        new(
            UtcNow,
            moviesWatched,
            0,
            showsStarted,
            0,
            [],
            movies,
            shows);
}
