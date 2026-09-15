using MovieApp.Application.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class ReleaseRegionOptionsValidatorTests
{
    private readonly ReleaseRegionOptionsValidator _validator = new();

    [Fact]
    public void Validate_AcceptsConfiguredTrRegion()
    {
        var result = _validator.Validate(null, new ReleaseRegionOptions { DefaultRegion = "TR" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RejectsEmptyRegion()
    {
        var result = _validator.Validate(null, new ReleaseRegionOptions { DefaultRegion = "" });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_RejectsInvalidRegionFormat()
    {
        var result = _validator.Validate(null, new ReleaseRegionOptions { DefaultRegion = "TUR" });

        Assert.False(result.Succeeded);
    }
}
