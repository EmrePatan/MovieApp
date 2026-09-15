using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.TvShows;

public sealed class TvShowFollowActionEligibilityTests
{
    [Fact]
    public void CanFollow_ReturningSeries_ReturnsTrue()
    {
        Assert.True(TvShowFollowActionEligibility.CanFollow(TvShowStatus.ReturningSeries));
    }

    [Fact]
    public void CanFollow_InProduction_ReturnsTrue()
    {
        Assert.True(TvShowFollowActionEligibility.CanFollow(TvShowStatus.InProduction));
    }

    [Fact]
    public void CanFollow_Planned_ReturnsTrue()
    {
        Assert.True(TvShowFollowActionEligibility.CanFollow(TvShowStatus.Planned));
    }

    [Fact]
    public void CanFollow_Pilot_ReturnsTrue()
    {
        Assert.True(TvShowFollowActionEligibility.CanFollow(TvShowStatus.Pilot));
    }

    [Fact]
    public void CanFollow_Ended_ReturnsFalse()
    {
        Assert.False(TvShowFollowActionEligibility.CanFollow(TvShowStatus.Ended));
    }

    [Fact]
    public void CanFollow_Canceled_ReturnsFalse()
    {
        Assert.False(TvShowFollowActionEligibility.CanFollow(TvShowStatus.Canceled));
    }

    [Fact]
    public void CanFollow_DefaultStatus_ReturnsTrue()
    {
        Assert.True(TvShowFollowActionEligibility.CanFollow(default));
    }

    [Fact]
    public void CanFollow_Ended_DoesNotInferFromLastAirDate()
    {
        Assert.False(TvShowFollowActionEligibility.CanFollow(TvShowStatus.Ended));
    }
}
