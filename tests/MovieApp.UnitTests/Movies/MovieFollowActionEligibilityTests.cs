using MovieApp.Application.Services.Movies;

namespace MovieApp.UnitTests.Movies;

public sealed class MovieFollowActionEligibilityTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    [Fact]
    public void CanFollowForRelease_FutureEffectiveDate_ReturnsTrue()
    {
        Assert.True(MovieFollowActionEligibility.CanFollowForRelease(Today.AddDays(1), Today));
    }

    [Fact]
    public void CanFollowForRelease_PastEffectiveDate_ReturnsFalse()
    {
        Assert.False(MovieFollowActionEligibility.CanFollowForRelease(Today.AddDays(-1), Today));
    }

    [Fact]
    public void CanFollowForRelease_TodayEffectiveDate_ReturnsFalse()
    {
        Assert.False(MovieFollowActionEligibility.CanFollowForRelease(Today, Today));
    }

    [Fact]
    public void CanFollowForRelease_NullEffectiveDate_ReturnsTrue()
    {
        Assert.True(MovieFollowActionEligibility.CanFollowForRelease(null, Today));
    }

    [Fact]
    public void CanSetReleaseAlert_MatchesCanFollowForRelease()
    {
        Assert.True(MovieFollowActionEligibility.CanSetReleaseAlert(Today.AddDays(7), Today));
        Assert.False(MovieFollowActionEligibility.CanSetReleaseAlert(Today, Today));
        Assert.True(MovieFollowActionEligibility.CanSetReleaseAlert(null, Today));
    }

    [Fact]
    public void CanFollowForRelease_RegionalFutureOverridesGlobalPast_ReturnsTrue()
    {
        Assert.True(MovieFollowActionEligibility.CanFollowForRelease(Today.AddDays(30), Today));
    }
}
