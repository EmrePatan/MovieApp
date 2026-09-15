using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Discovery;

public sealed class NowInTheatersValidatorTests
{
    [Fact]
    public void ValidateAcceptsExplicitReleaseRegion()
    {
        var result = NowInTheatersValidator.Validate(CreateCriteria(releaseRegion: "us"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidReleaseRegion()
    {
        var result = NowInTheatersValidator.Validate(CreateCriteria(releaseRegion: "TUR"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidPageSize()
    {
        var result = NowInTheatersValidator.Validate(CreateCriteria(pageSize: 0));

        Assert.False(result.IsValid);
    }

    private static NowInTheatersCriteria CreateCriteria(
        string releaseRegion = "TR",
        int page = 1,
        int pageSize = 20) =>
        new(releaseRegion, page, pageSize);
}
