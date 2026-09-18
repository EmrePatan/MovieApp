using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsV3BuilderTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

    private static readonly DateTime UtcNow = new(2026, 6, 15, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void YourYearRespectsCalendarYearBoundariesInTimeZone()
    {
        var year = 2026;
        var raw = CreateRawData(
            yearMovieWatches:
            [
                (new DateTime(2025, 12, 31, 21, 30, 0, DateTimeKind.Utc), 120),
                (new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc), 120),
                (new DateTime(2026, 12, 31, 20, 30, 0, DateTimeKind.Utc), 120),
                (new DateTime(2027, 1, 1, 0, 30, 0, DateTimeKind.Utc), 120),
            ],
            yearEpisodeWatches: []);

        var yourYear = InsightsV3YourYearBuilder.Build(raw, Istanbul, year);

        Assert.Equal(2, yourYear.ActiveDays);
        Assert.Equal(2, yourYear.Months.Single(month => month.Month == 1).Movies);
        Assert.Equal(1, yourYear.Months.Single(month => month.Month == 12).Movies);
        Assert.Equal(2, yourYear.PeakMonth?.Total);
    }

    [Fact]
    public void WatchingMixUsesDistinctMovieAndSeriesCounts()
    {
        var mix = InsightsV3MovieDnaBuilder.BuildWatchingMix(4, 1);

        Assert.Equal(4, mix.MovieTitleCount);
        Assert.Equal(1, mix.SeriesTitleCount);
        Assert.Equal(80.0m, mix.MovieSharePercent);
        Assert.Equal(20.0m, mix.SeriesSharePercent);
    }

    [Fact]
    public void RecordsUsePrecomputedSqlAggregates()
    {
        var raw = CreateRawData(
            records: new InsightsV3RecordsRawData(
                4,
                new InsightsV3WeeklyPeakResult(2026, 1, 3),
                new InsightsV3WeeklyPeakResult(2026, 1, 1)),
            ratingScoreCounts: [(10, 1), (8, 2)]);

        var records = InsightsV3RecordsBuilder.Build(raw, TimeZoneInfo.Utc);

        Assert.Equal(4, records.LongestStreakDays);
        Assert.Equal(3, records.BestMovieWeek?.Count);
        Assert.Equal(1, records.BestEpisodeWeek?.Count);
        Assert.Equal(5.0m, records.HighestRatingStars);
    }

    [Fact]
    public void RatingsRequireMinimumGenreSampleBeforeReturningHighLowGenres()
    {
        var dramaId = Guid.NewGuid();
        var raw = CreateRawData(
            genreRatings:
            [
                new InsightsV3GenreRatingRow(dramaId, "Drama", 2, 8m),
                new InsightsV3GenreRatingRow(Guid.NewGuid(), "Comedy", 3, 6m),
            ]);

        var ratings = InsightsV3RatingsBuilder.Build(raw);

        Assert.Null(ratings.HighestRatedGenre);
        Assert.NotNull(ratings.LowestRatedGenre);
        Assert.Equal("Comedy", ratings.LowestRatedGenre!.Name);
    }

    [Fact]
    public void RisingGenreReturnsNullWhenYearHistoryIsInsufficient()
    {
        var sciFi = new InsightsDnaGenreData(Guid.NewGuid(), "Sci-Fi");
        var rising = InsightsV3TasteBuilder.TryBuildRisingGenre(
            [new InsightsDnaTitleData(2026, [sciFi]), new InsightsDnaTitleData(2026, [sciFi])],
            [],
            [new InsightsDnaTitleData(2025, [sciFi]), new InsightsDnaTitleData(2025, [sciFi]), new InsightsDnaTitleData(2025, [sciFi])],
            []);

        Assert.Null(rising);
    }

    [Fact]
    public void RisingGenreReturnsCandidateWhenBothYearsHaveSufficientData()
    {
        var sciFi = new InsightsDnaGenreData(Guid.NewGuid(), "Sci-Fi");
        var drama = new InsightsDnaGenreData(Guid.NewGuid(), "Drama");
        var current = Enumerable.Repeat(new InsightsDnaTitleData(2026, [sciFi, drama]), 3).ToList();
        var previous = Enumerable.Repeat(new InsightsDnaTitleData(2025, [drama]), 3).ToList();

        var rising = InsightsV3TasteBuilder.TryBuildRisingGenre(current, [], previous, []);

        Assert.NotNull(rising);
        Assert.Equal("Sci-Fi", rising!.Name);
        Assert.True(rising.ShareDeltaPercent >= InsightsV3TasteBuilder.MinimumShareDeltaPercent);
    }

    private static InsightsV3RawData CreateRawData(
        IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)>? yearMovieWatches = null,
        IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)>? yearEpisodeWatches = null,
        InsightsV3RecordsRawData? records = null,
        IReadOnlyList<(int Score, int Count)>? ratingScoreCounts = null,
        IReadOnlyList<InsightsV3GenreRatingRow>? genreRatings = null)
    {
        var milestoneRaw = new InsightsAnalyticsRawData(
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            0,
            0,
            0,
            ratingScoreCounts?.Sum(item => item.Count) ?? 0,
            [],
            [],
            [],
            0,
            0,
            0,
            0,
            ratingScoreCounts ?? [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        return new InsightsV3RawData(
            milestoneRaw.MemberSinceUtc,
            0,
            0,
            0,
            0,
            0,
            milestoneRaw.RatingsCount,
            [],
            [],
            [],
            [],
            [],
            [],
            yearMovieWatches ?? [],
            yearEpisodeWatches ?? [],
            records ?? new InsightsV3RecordsRawData(null, null, null),
            0,
            0,
            0,
            0,
            ratingScoreCounts ?? [],
            genreRatings ?? [],
            null,
            milestoneRaw);
    }
}
