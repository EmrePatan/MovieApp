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
