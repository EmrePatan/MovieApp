using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class AchievementPointsTests
{
    [Fact]
    public void SumCountsOnlyAchievedTiersWithTheirWeights()
    {
        var achievements = new List<InsightsMilestoneResult>
        {
            new("movies-1", "movies", "t", 10, 1, true, null, 1),
            new("movies-10", "movies", "t", 10, 10, true, null, 2),
            new("movies-50", "movies", "t", 10, 50, false, null, 3),
            new("hours-500", "hours", "t", 1000, 500, true, null, 4),
            new("streak-30", "streak", "t", 30, 30, true, null, 5),
        };

        Assert.Equal(1 + 2 + 5 + 8, AchievementPoints.Sum(achievements));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 8)]
    [InlineData(9, 8)]
    public void ForTierClampsToTheLadder(int tier, int expected) =>
        Assert.Equal(expected, AchievementPoints.ForTier(tier));
}
