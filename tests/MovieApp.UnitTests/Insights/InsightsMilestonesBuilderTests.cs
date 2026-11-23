using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsMilestonesBuilderTests
{
    [Fact]
    public void BuildMarksMovieThresholdEdges()
    {
        var raw = CreateRaw(moviesWatched: 10, firstMovieAt: DateTime.UtcNow);

        var milestones = InsightsMilestonesBuilder.Build(raw);

        Assert.True(milestones.Single(item => item.Id == "movies-10").Achieved);
        Assert.False(milestones.Single(item => item.Id == "movies-50").Achieved);
    }

    [Fact]
    public void BuildUsesCompletionProjectionForCompletedShows()
    {
        var completedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var raw = CreateRaw(
            showCompletions:
            [
                new InsightsShowCompletionData(10, 10, completedAt, IsConcluded: true),
                new InsightsShowCompletionData(5, 5, completedAt.AddDays(1), IsConcluded: true),
                new InsightsShowCompletionData(8, 8, completedAt.AddDays(2), IsConcluded: true),
            ]);

        var milestones = InsightsMilestonesBuilder.Build(raw);

        Assert.True(milestones.Single(item => item.Id == "first-show-completed").Achieved);
        Assert.Equal(completedAt, milestones.Single(item => item.Id == "first-show-completed").AchievedAt);
        Assert.True(milestones.Single(item => item.Id == "shows-completed-3").Achieved);
        Assert.Equal(completedAt.AddDays(2), milestones.Single(item => item.Id == "shows-completed-3").AchievedAt);
    }

    [Fact]
    public void BuildDoesNotCountCaughtUpReturningShowsAsCompleted()
    {
        var caughtUpAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var raw = CreateRaw(
            showCompletions:
            [
                new InsightsShowCompletionData(10, 10, caughtUpAt, IsConcluded: false),
            ]);

        Assert.Equal(0, InsightsMilestonesBuilder.CountCompletedShows(raw));
        Assert.False(InsightsMilestonesBuilder.Build(raw).Single(item => item.Id == "first-show-completed").Achieved);
    }

    [Fact]
    public void BuildLeavesGenreMilestoneAchievedAtNull()
    {
        var genreId = Guid.NewGuid();
        var raw = CreateRaw(
            movieTitles:
            [
                new InsightsDnaTitleData(2020, [new InsightsDnaGenreData(genreId, "Action")]),
            ],
            tvTitles:
            [
                new InsightsDnaTitleData(2021, [new InsightsDnaGenreData(Guid.NewGuid(), "Drama")]),
                new InsightsDnaTitleData(2022, [new InsightsDnaGenreData(Guid.NewGuid(), "Comedy")]),
                new InsightsDnaTitleData(2023, [new InsightsDnaGenreData(Guid.NewGuid(), "Sci-Fi")]),
                new InsightsDnaTitleData(2024, [new InsightsDnaGenreData(Guid.NewGuid(), "Horror")]),
            ]);

        var milestone = InsightsMilestonesBuilder.Build(raw).Single(item => item.Id == "genres-5");

        Assert.True(milestone.Achieved);
        Assert.Null(milestone.AchievedAt);
    }

    [Fact]
    public void BuildUsesPrecomputedDistinctGenreCountWithoutTitleRows()
    {
        var raw = new InsightsAnalyticsRawData(
            DateTime.UtcNow,
            0,
            0,
            0,
            0,
            [],
            [],
            [],
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
            null,
            DistinctGenreCount: 5);

        var milestone = InsightsMilestonesBuilder.Build(raw).Single(item => item.Id == "genres-5");

        Assert.Equal(5, milestone.CurrentValue);
        Assert.True(milestone.Achieved);
        Assert.Null(milestone.AchievedAt);
    }

    [Fact]
    public void BuildCreatesTieredFamiliesWithSharedProgress()
    {
        var raw = CreateRaw(moviesWatched: 60);

        var milestones = InsightsMilestonesBuilder.Build(raw);
        var movies = milestones.Where(item => item.Category == "movies").OrderBy(item => item.Tier).ToList();

        Assert.Equal([1, 2, 3, 4, 5], movies.Select(item => item.Tier));
        Assert.Equal([1, 10, 50, 100, 250], movies.Select(item => item.TargetValue));
        Assert.All(movies, item => Assert.Equal(60, item.CurrentValue));
        Assert.Equal([true, true, true, false, false], movies.Select(item => item.Achieved));
    }

    [Fact]
    public void BuildKeepsLegacyIdsForTheOriginalMilestones()
    {
        var ids = InsightsMilestonesBuilder.Build(CreateRaw()).Select(item => item.Id).ToHashSet();

        foreach (var legacy in new[]
                 {
                     "first-movie", "movies-10", "movies-50", "episodes-100", "episodes-500", "ratings-10",
                     "ratings-25", "ratings-50", "first-show-completed", "shows-completed-3", "genres-5",
                 })
        {
            Assert.Contains(legacy, ids);
        }

        Assert.Equal(ids.Count, InsightsMilestonesBuilder.Build(CreateRaw()).Count);
    }

    private static InsightsAnalyticsRawData CreateRaw(
        int moviesWatched = 0,
        DateTime? firstMovieAt = null,
        IReadOnlyList<InsightsShowCompletionData>? showCompletions = null,
        IReadOnlyList<InsightsDnaTitleData>? movieTitles = null,
        IReadOnlyList<InsightsDnaTitleData>? tvTitles = null) =>
        new(
            DateTime.UtcNow,
            moviesWatched,
            0,
            0,
            0,
            [],
            movieTitles ?? [],
            tvTitles ?? [],
            0,
            0,
            0,
            0,
            [],
            showCompletions ?? [],
            firstMovieAt,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
}
