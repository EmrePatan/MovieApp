using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationCandidateBudgetTests
{
    [Fact]
    public void SplitReservesHalfTheBudgetForEachType()
    {
        var split = RecommendationCandidateBudget.Split(500, minorityFloor: 100);

        Assert.Equal(250, split.MovieBudget);
        Assert.Equal(250, split.TvBudget);
    }

    [Fact]
    public void SplitClampsTheMinorityFloorToHalfWhenTheBudgetIsSmall()
    {
        var split = RecommendationCandidateBudget.Split(10, minorityFloor: 100);

        Assert.Equal(5, split.MovieBudget);
        Assert.Equal(5, split.TvBudget);
    }
}
