using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Domain;

public sealed class TvShowStatusTests
{
    [Fact]
    public void TvShowStatusContainsExpectedProductionValues()
    {
        Assert.Equal(1, (int)TvShowStatus.ReturningSeries);
        Assert.Equal(4, (int)TvShowStatus.Ended);
        Assert.Equal(5, (int)TvShowStatus.Canceled);
    }
}
