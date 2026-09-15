using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Discovery;

public sealed class WorldCinemaValidatorTests
{
    [Fact]
    public void ValidateAcceptsMovieWithOriginCountry()
    {
        var result = WorldCinemaValidator.Validate(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 20));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsMissingOriginCountry()
    {
        var result = WorldCinemaValidator.ValidateRequiredOriginCountry(null);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidOriginCountry()
    {
        var result = WorldCinemaValidator.ValidateRequiredOriginCountry("KOR");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidPageSize()
    {
        var result = WorldCinemaValidator.Validate(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 0));

        Assert.False(result.IsValid);
    }
}
