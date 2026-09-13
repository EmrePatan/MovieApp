using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class ProfileStatisticsBuilderTests
{
    [Fact]
    public void BuildReturnsEmptyInsightsForNewUser()
    {
        var result = ProfileStatisticsBuilder.Build(CreateRawData(), new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0, result.Summary.MoviesWatched);
        Assert.Equal(12, result.Activity.Last12Months.Count);
        Assert.Empty(result.Genres);
        Assert.Empty(result.Milestones);
        Assert.Empty(result.Insights);
        Assert.Null(result.Activity.LongestStreakDays);
    }

    [Fact]
    public void BuildCalculatesStarDistributionAndAverage()
    {
        var result = ProfileStatisticsBuilder.Build(
            CreateRawData(
                ratedMovieCount: 2,
                ratedTvShowCount: 1,
                ratingScoreCounts: [(10, 2), (6, 1)]),
            new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(3, result.Summary.RatingsCount);
        Assert.Equal(4.3m, result.Ratings.AverageStarRating);
        Assert.Equal(5, result.Ratings.MostUsedStars);
        Assert.Equal(2, result.Ratings.Distribution.Single(item => item.Stars == 5).Count);
        Assert.Equal(1, result.Ratings.Distribution.Single(item => item.Stars == 3).Count);
    }

    [Fact]
    public void BuildCalculatesLongestStreakFromDistinctDates()
    {
        var result = ProfileStatisticsBuilder.Build(
            CreateRawData(
                distinctWatchDates:
                [
                    new DateOnly(2026, 4, 1),
                    new DateOnly(2026, 4, 2),
                    new DateOnly(2026, 4, 3),
                    new DateOnly(2026, 4, 10),
                ]),
            new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(3, result.Activity.LongestStreakDays);
    }

    [Fact]
    public void BuildAddsMilestonesWhenThresholdsAreMet()
    {
        var result = ProfileStatisticsBuilder.Build(
            CreateRawData(
                watchedMovieCount: 10,
                watchedEpisodeCount: 120,
                ratedMovieCount: 10,
                showsCompleted: 1,
                firstMovieWatchedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains(result.Milestones, milestone => milestone.Id == "movies-10");
        Assert.Contains(result.Milestones, milestone => milestone.Id == "episodes-100");
        Assert.Contains(result.Milestones, milestone => milestone.Id == "ratings-10");
        Assert.Contains(result.Milestones, milestone => milestone.Id == "first-show-completed");
    }

    [Fact]
    public void BuildCreatesDeterministicInsights()
    {
        var result = ProfileStatisticsBuilder.Build(
            CreateRawData(
                watchedMovieCount: 4,
                watchedEpisodeCount: 16,
                showsStarted: 4,
                ratedMovieCount: 6,
                genres:
                [
                    new GenreStatisticResult(Guid.NewGuid(), "Comedy", 12),
                    new GenreStatisticResult(Guid.NewGuid(), "Sci-Fi", 8),
                ],
                monthlyActivity:
                [
                    new MonthlyActivityResult(2026, 3, 2, 10, 12),
                    new MonthlyActivityResult(2026, 4, 1, 7, 8),
                ],
                ratingScoreCounts: [(8, 6)]),
            new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains(result.Insights, insight => insight.Contains("Comedy", StringComparison.Ordinal));
        Assert.Contains(result.Insights, insight => insight.Contains("March", StringComparison.Ordinal));
        Assert.Contains(result.Insights, insight => insight.Contains("4★"));
    }

    private static ProfileStatisticsRawData CreateRawData(
        int favoriteMovieCount = 0,
        int favoriteTvShowCount = 0,
        int watchlistCount = 0,
        int watchlistItemCount = 0,
        int ratedMovieCount = 0,
        int ratedTvShowCount = 0,
        int reviewedMovieCount = 0,
        int reviewedTvShowCount = 0,
        int watchedMovieCount = 0,
        int watchedEpisodeCount = 0,
        int showsStarted = 0,
        int showsCompleted = 0,
        IReadOnlyList<MonthlyActivityResult>? monthlyActivity = null,
        IReadOnlyList<DateOnly>? distinctWatchDates = null,
        IReadOnlyList<GenreStatisticResult>? genres = null,
        IReadOnlyList<(int Score, int Count)>? ratingScoreCounts = null,
        DateTime? firstMovieWatchedAt = null,
        DateTime? firstCompletedShowAt = null) =>
        new(
            favoriteMovieCount,
            favoriteTvShowCount,
            watchlistCount,
            watchlistItemCount,
            ratedMovieCount,
            ratedTvShowCount,
            reviewedMovieCount,
            reviewedTvShowCount,
            watchedMovieCount,
            watchedEpisodeCount,
            showsStarted,
            showsCompleted,
            monthlyActivity ?? [],
            distinctWatchDates ?? [],
            genres ?? [],
            ratingScoreCounts ?? [],
            firstMovieWatchedAt,
            firstCompletedShowAt);
}
