using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsMovieDnaBuilderTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildReturnsNoLabelsWhenUniqueTitlesBelowFive()
    {
        var raw = CreateRawData(
            moviesWatched: 2,
            showsStarted: 2);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        Assert.Empty(labels);
    }

    [Fact]
    public void BuildReturnsNoGenreLabelWhenTopGenreShareBelowThreshold()
    {
        var raw = CreateRawData(
            moviesWatched: 5,
            movieTitles:
            [
                Title(2024, [Genre("Action")]),
                Title(2024, [Genre("Drama")]),
                Title(2024, [Genre("Comedy")]),
                Title(2024, [Genre("Thriller")]),
                Title(2024, [Genre("Western")]),
            ]);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        Assert.DoesNotContain(labels, label => label.Code == InsightsMovieDnaBuilder.TopGenreCode);
    }

    [Fact]
    public void BuildReturnsTopGenreLabelAtThreshold()
    {
        var sciFi = Genre("Sci-Fi");
        var drama = Genre("Drama");
        var raw = CreateRawData(
            moviesWatched: 5,
            movieTitles:
            [
                Title(2024, [sciFi]),
                Title(2024, [sciFi]),
                Title(2024, [sciFi]),
                Title(2024, [sciFi]),
                Title(2024, [drama]),
            ]);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        var genreLabel = Assert.Single(labels, label => label.Code == InsightsMovieDnaBuilder.TopGenreCode);
        Assert.Equal(InsightsMovieDnaBuilder.GenreCategory, genreLabel.Category);
        Assert.Equal("Sci-Fi", genreLabel.Label);
    }

    [Fact]
    public void BuildUsesFractionalWeightingForMultiGenreTitles()
    {
        var action = Genre("Action");
        var sciFi = Genre("Sci-Fi");
        var drama = Genre("Drama");
        var raw = CreateRawData(
            moviesWatched: 5,
            movieTitles:
            [
                Title(2024, [action, sciFi]),
                Title(2024, [action, sciFi]),
                Title(2024, [action, sciFi]),
                Title(2024, [action, sciFi]),
                Title(2024, [drama]),
            ]);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        var genreLabel = Assert.Single(labels, label => label.Code == InsightsMovieDnaBuilder.TopGenreCode);
        Assert.Equal("Action", genreLabel.Label);
    }

    [Fact]
    public void BuildBreaksGenreTiesDeterministicallyByNameThenId()
    {
        var raw = CreateRawData(
            moviesWatched: 6,
            movieTitles:
            [
                Title(2024, [Genre("Alpha", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))]),
                Title(2024, [Genre("Alpha", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))]),
                Title(2024, [Genre("Alpha", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))]),
                Title(2024, [Genre("Beta", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))]),
                Title(2024, [Genre("Beta", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))]),
                Title(2024, [Genre("Beta", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))]),
            ]);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        var genreLabel = Assert.Single(labels, label => label.Code == InsightsMovieDnaBuilder.TopGenreCode);
        Assert.Equal("Alpha", genreLabel.Label);
    }

    [Fact]
    public void BuildReturnsSeriesFirstWhenSeriesShareAtLeastSixtyFivePercent()
    {
        var raw = CreateRawData(
            moviesWatched: 1,
            showsStarted: 4);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        var formatLabel = Assert.Single(labels, label => label.Category == InsightsMovieDnaBuilder.FormatCategory);
        Assert.Equal(InsightsMovieDnaBuilder.SeriesFirstCode, formatLabel.Code);
        Assert.Equal("Series-first", formatLabel.Label);
    }

    [Fact]
    public void BuildReturnsMovieFirstWhenSeriesShareAtMostThirtyFivePercent()
    {
        var raw = CreateRawData(
            moviesWatched: 4,
            showsStarted: 1);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        var formatLabel = Assert.Single(labels, label => label.Category == InsightsMovieDnaBuilder.FormatCategory);
        Assert.Equal(InsightsMovieDnaBuilder.MovieFirstCode, formatLabel.Code);
        Assert.Equal("Movie-first", formatLabel.Label);
    }

    [Fact]
    public void BuildReturnsNoFormatLabelInMiddleRange()
    {
        var raw = CreateRawData(
            moviesWatched: 3,
            showsStarted: 2);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        Assert.DoesNotContain(labels, label => label.Category == InsightsMovieDnaBuilder.FormatCategory);
    }

    [Fact]
    public void BuildReturnsRecentReleasesWhenThresholdMet()
    {
        var action = Genre("Action");
        var raw = CreateRawData(
            moviesWatched: 5,
            movieTitles:
            [
                Title(2026, [action]),
                Title(2025, [action]),
                Title(2024, [action]),
                Title(2023, [action]),
                Title(2010, [action]),
            ]);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        var eraLabel = Assert.Single(labels, label => label.Code == InsightsMovieDnaBuilder.RecentReleasesCode);
        Assert.Equal("Recent releases", eraLabel.Label);
    }

    [Fact]
    public void BuildExcludesMissingReleaseYearFromRecentReleasesDenominator()
    {
        var action = Genre("Action");
        var raw = CreateRawData(
            moviesWatched: 7,
            movieTitles:
            [
                Title(2026, [action]),
                Title(2025, [action]),
                Title(2024, [action]),
                Title(2023, [action]),
                Title(null, [action]),
                Title(null, [action]),
                Title(1990, [action]),
            ]);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        Assert.Contains(labels, label => label.Code == InsightsMovieDnaBuilder.RecentReleasesCode);
    }

    [Fact]
    public void BuildReturnsMaximumThreeLabelsInPriorityOrder()
    {
        var sciFi = Genre("Sci-Fi");
        var drama = Genre("Drama");
        var raw = CreateRawData(
            moviesWatched: 5,
            showsStarted: 0,
            movieTitles:
            [
                Title(2026, [sciFi]),
                Title(2026, [sciFi]),
                Title(2026, [sciFi]),
                Title(2026, [sciFi]),
                Title(2026, [drama]),
            ]);

        var labels = InsightsMovieDnaBuilder.Build(raw, UtcNow);

        Assert.Equal(3, labels.Count);
        Assert.Equal(InsightsMovieDnaBuilder.TopGenreCode, labels[0].Code);
        Assert.Equal(InsightsMovieDnaBuilder.MovieFirstCode, labels[1].Code);
        Assert.Equal(InsightsMovieDnaBuilder.RecentReleasesCode, labels[2].Code);
    }

    private static InsightsDnaGenreData Genre(string name, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name);

    private static InsightsDnaTitleData Title(int? releaseYear, IReadOnlyList<InsightsDnaGenreData> genres) =>
        new(releaseYear, genres);

    private static InsightsSummaryRawData CreateRawData(
        int moviesWatched = 0,
        int showsStarted = 0,
        IReadOnlyList<InsightsDnaTitleData>? movieTitles = null,
        IReadOnlyList<InsightsDnaTitleData>? tvShowTitles = null) =>
        new(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            moviesWatched,
            0,
            showsStarted,
            0,
            [],
            movieTitles ?? [],
            tvShowTitles ?? []);
}
