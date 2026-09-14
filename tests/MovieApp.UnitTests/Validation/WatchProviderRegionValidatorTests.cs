using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class WatchProviderRegionValidatorTests
{
    [Fact]
    public void NormalizeDefaultsToTrWhenMissing()
    {
        Assert.Equal("TR", WatchProviderRegionValidator.Normalize(null));
    }

    [Fact]
    public void ValidateRejectsInvalidRegion()
    {
        var result = WatchProviderRegionValidator.Validate("turkey");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsTwoLetterRegion()
    {
        var result = WatchProviderRegionValidator.Validate("us");

        Assert.True(result.IsValid);
        Assert.Equal("US", WatchProviderRegionValidator.Normalize("us"));
    }
}
