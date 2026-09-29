using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsV3BuilderTests
{
    [Fact]
    public void YourYearFillsTwelveMonthsAndBreaksPeakTiesTowardLaterMonth()
    {
        var raw = CreateRawData(
            yearActivity: new InsightsV3YearActivityAggregate(
                [
                    new InsightsV3MonthCount(1, 2, 0),
                    new InsightsV3MonthCount(3, 1, 1),
                ],
                new Dictionary<DayOfWeek, int>(),
                2,
                0));

        var yourYear = InsightsV3YourYearBuilder.Build(raw, 2026);

        Assert.Equal(12, yourYear.Months.Count);
        Assert.Equal(2, yourYear.Months.Single(month => month.Month == 1).Movies);
        Assert.Equal(0, yourYear.Months.Single(month => month.Month == 2).Total);
        Assert.Equal(2, yourYear.ActiveDays);
        Assert.Equal(3, yourYear.PeakMonth?.Month);
        Assert.Equal(2, yourYear.PeakMonth?.Total);
    }

    [Fact]
    public void FavoriteWeekdayUsesWeekdayTotalsAndActivityThreshold()
    {
        var dominant = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 30,
            [DayOfWeek.Tuesday] = 2,
            [DayOfWeek.Wednesday] = 2,
            [DayOfWeek.Thursday] = 2,
            [DayOfWeek.Friday] = 2,
            [DayOfWeek.Saturday] = 2,
            [DayOfWeek.Sunday] = 2,
        };
        var belowDominance = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 10,
            [DayOfWeek.Tuesday] = 8,
            [DayOfWeek.Wednesday] = 8,
            [DayOfWeek.Thursday] = 8,
            [DayOfWeek.Friday] = 8,
            [DayOfWeek.Saturday] = 8,
            [DayOfWeek.Sunday] = 8,
        };

        Assert.Equal(DayOfWeek.Monday, InsightsV3YourYearBuilder.CalculateFavoriteWeekday(dominant));
        Assert.Null(InsightsV3YourYearBuilder.CalculateFavoriteWeekday(belowDominance));
        Assert.Null(InsightsV3YourYearBuilder.CalculateFavoriteWeekday(
            new Dictionary<DayOfWeek, int> { [DayOfWeek.Friday] = 9 }));
    }

    [Fact]
    public void TimeInStoriesUsesAggregatedYearMinutes()
    {
        var raw = CreateRawData(
            yearActivity: new InsightsV3YearActivityAggregate([], new Dictionary<DayOfWeek, int>(), 0, 235),
            movieEstimatedMinutes: 400,
            episodeEstimatedMinutes: 80,
            moviesWithKnownRuntime: 2,
            episodesWithKnownRuntime: 2,
            moviesWatched: 3,
            episodesWatched: 2);

        var time = InsightsV3TimeInStoriesBuilder.Build(raw);

        Assert.Equal(480, time.TotalMinutes);
        Assert.Equal(235, time.YearMinutes);
        Assert.Equal(80.0m, time.RuntimeCoveragePercent);
    }

    [Fact]
    public void RisingGenreGroupedContributionsMatchPerTitleShares()
    {
        var sciFi = new InsightsDnaGenreData(Guid.NewGuid(), "Sci-Fi");
        var drama = new InsightsDnaGenreData(Guid.NewGuid(), "Drama");
        var currentTitles = Enumerable.Repeat(new InsightsDnaTitleData(2026, [sciFi, drama]), 3).ToList();
        var previousTitles = Enumerable.Repeat(new InsightsDnaTitleData(2025, [drama]), 3).ToList();

        var fromTitles = InsightsV3TasteBuilder.TryBuildRisingGenre(currentTitles, [], previousTitles, []);
        var fromGroups = InsightsV3TasteBuilder.TryBuildRisingGenre(
            [
                new InsightsV3GenreContribution(sciFi.GenreId, sciFi.Name, 2, 3),
                new InsightsV3GenreContribution(drama.GenreId, drama.Name, 2, 3),
            ],
            3,
            [new InsightsV3GenreContribution(drama.GenreId, drama.Name, 1, 3)],
            3);

        Assert.Equal(fromTitles, fromGroups);
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
        InsightsV3YearActivityAggregate? yearActivity = null,
        InsightsV3RecordsRawData? records = null,
        IReadOnlyList<(int Score, int Count)>? ratingScoreCounts = null,
        IReadOnlyList<InsightsV3GenreRatingRow>? genreRatings = null,
        int movieEstimatedMinutes = 0,
        int episodeEstimatedMinutes = 0,
        int moviesWithKnownRuntime = 0,
        int episodesWithKnownRuntime = 0,
        int moviesWatched = 0,
        int episodesWatched = 0)
    {
        var milestoneRaw = new InsightsAnalyticsRawData(
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            moviesWatched,
            episodesWatched,
            0,
            ratingScoreCounts?.Sum(item => item.Count) ?? 0,
            [],
            [],
            [],
            movieEstimatedMinutes,
            moviesWithKnownRuntime,
            episodeEstimatedMinutes,
            episodesWithKnownRuntime,
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
            moviesWatched,
            episodesWatched,
            0,
            milestoneRaw.RatingsCount,
            [],
            0,
            [],
            0,
            [],
            0,
            [],
            0,
            yearActivity ?? new InsightsV3YearActivityAggregate([], new Dictionary<DayOfWeek, int>(), 0, 0),
            records ?? new InsightsV3RecordsRawData(null, null, null),
            movieEstimatedMinutes,
            episodeEstimatedMinutes,
            moviesWithKnownRuntime,
            episodesWithKnownRuntime,
            ratingScoreCounts ?? [],
            genreRatings ?? [],
            null,
            milestoneRaw);
    }
}
