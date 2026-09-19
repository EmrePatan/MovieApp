using MovieApp.Application.Models.Insights;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsV3DnaProjectionsTests
{
    private static readonly Guid ShowA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ShowB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime CurrentYearStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CurrentYearEnd = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PreviousYearStart = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PreviousYearEnd = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MovieProjectionsDeriveAllTimeSplitAndYearActivityFromSingleRowset()
    {
        var rows = new List<InsightsV3DnaProjections.MovieWatchRow>
        {
            CreateMovieRow(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), 120, 2020),
            CreateMovieRow(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), 90, 2021),
            CreateMovieRow(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), 100, 2022),
        };

        var allTime = InsightsV3DnaProjections.ToAllTimeMovieTitles(rows);
        var split = InsightsV3DnaProjections.SplitMovieTitlesByYear(
            rows,
            PreviousYearStart,
            PreviousYearEnd,
            CurrentYearStart,
            CurrentYearEnd);
        var yearActivity = InsightsV3DnaProjections.ToYearMovieWatches(
            rows,
            CurrentYearStart,
            CurrentYearEnd);

        Assert.Equal(3, allTime.Count);
        Assert.Single(split.CurrentYear);
        Assert.Single(split.PreviousYear);
        Assert.Single(yearActivity);
        Assert.Equal(100, yearActivity[0].Item2);
    }

    [Fact]
    public void TvProjectionsUseDistinctShowsForAllTimeAndPerWatchForYearSplit()
    {
        var rows = new List<InsightsV3DnaProjections.EpisodeWatchRow>
        {
            CreateEpisodeRow(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), ShowA, 2010),
            CreateEpisodeRow(new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc), ShowA, 2010),
            CreateEpisodeRow(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), ShowB, 2015),
        };

        var allTime = InsightsV3DnaProjections.ToAllTimeTvShowTitles(rows);
        var split = InsightsV3DnaProjections.SplitTvShowTitlesByYear(
            rows,
            PreviousYearStart,
            PreviousYearEnd,
            CurrentYearStart,
            CurrentYearEnd);

        Assert.Equal(2, allTime.Count);
        Assert.Equal(2, split.PreviousYear.Count);
        Assert.Single(split.CurrentYear);
    }

    private static InsightsV3DnaProjections.MovieWatchRow CreateMovieRow(
        DateTime watchedAt,
        int runtime,
        int releaseYear) =>
        new(
            watchedAt,
            runtime,
            releaseYear,
            [new InsightsDnaGenreData(Guid.NewGuid(), "Drama")]);

    private static InsightsV3DnaProjections.EpisodeWatchRow CreateEpisodeRow(
        DateTime watchedAt,
        Guid tvShowId,
        int firstAirYear) =>
        new(
            watchedAt,
            45,
            tvShowId,
            firstAirYear,
            [new InsightsDnaGenreData(Guid.NewGuid(), "Comedy")]);
}
