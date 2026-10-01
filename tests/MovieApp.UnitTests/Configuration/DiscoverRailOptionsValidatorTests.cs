using MovieApp.Application.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class DiscoverRailOptionsValidatorTests
{
    [Fact]
    public void ValidatorsAcceptZeroAndDefaultThresholds()
    {
        Assert.True(new AdvancedDiscoverOptionsValidator()
            .Validate(null, new AdvancedDiscoverOptions { MinVoteCountWhenWatchProvider = 0 })
            .Succeeded);
        Assert.True(new NewReleasesOptionsValidator()
            .Validate(null, new NewReleasesOptions { MaxAgeDays = 0, MinVoteCountMovie = 0, MinVoteCountTv = 0 })
            .Succeeded);
        Assert.True(new NowInTheatersOptionsValidator()
            .Validate(null, new NowInTheatersOptions())
            .Succeeded);
        Assert.True(new TopRatedOptionsValidator()
            .Validate(null, new TopRatedOptions())
            .Succeeded);

        Assert.Equal(200, new AdvancedDiscoverOptions().MinVoteCountWhenWatchProvider);
        Assert.Equal(90, new NewReleasesOptions().MaxAgeDays);
        Assert.Equal(75, new NewReleasesOptions().MinVoteCountMovie);
        Assert.Equal(50, new NewReleasesOptions().MinVoteCountTv);
        Assert.Equal(60, new NowInTheatersOptions().MaxAgeDays);
    }

    [Fact]
    public void ValidatorsRejectNegativeThresholds()
    {
        Assert.False(new AdvancedDiscoverOptionsValidator()
            .Validate(null, new AdvancedDiscoverOptions { MinVoteCountWhenWatchProvider = -1 })
            .Succeeded);
        Assert.False(new NewReleasesOptionsValidator()
            .Validate(null, new NewReleasesOptions { MaxAgeDays = -1 })
            .Succeeded);
        Assert.False(new NewReleasesOptionsValidator()
            .Validate(null, new NewReleasesOptions { MinVoteCountMovie = -1 })
            .Succeeded);
        Assert.False(new NewReleasesOptionsValidator()
            .Validate(null, new NewReleasesOptions { MinVoteCountTv = -1 })
            .Succeeded);
        Assert.False(new NowInTheatersOptionsValidator()
            .Validate(null, new NowInTheatersOptions { MaxAgeDays = -1 })
            .Succeeded);
        Assert.False(new TopRatedOptionsValidator()
            .Validate(null, new TopRatedOptions { MinVoteCountMovie = -1, MinVoteCountTv = -1 })
            .Succeeded);
        Assert.False(new AdvancedDiscoverOptionsValidator()
            .Validate(null, new AdvancedDiscoverOptions { TransactionalWatchProviderIds = [0] })
            .Succeeded);
    }
}
