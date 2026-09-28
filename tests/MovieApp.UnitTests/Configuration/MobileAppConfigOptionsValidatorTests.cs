using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Configuration;

public sealed class MobileAppConfigOptionsValidatorTests
{
    private readonly MobileAppConfigOptionsValidator _validator = new();

    [Fact]
    public void ValidateAcceptsDefaultSafeConfiguration()
    {
        var options = new MobileAppConfigOptions();

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateRejectsLatestBuildBelowMinimum()
    {
        var options = new MobileAppConfigOptions
        {
            Versions = new MobileAppConfigVersionsOptions
            {
                Ios = new MobileAppConfigPlatformVersionOptions
                {
                    MinimumBuild = 10,
                    LatestBuild = 9,
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ValidateRejectsNegativeBuildNumbers()
    {
        var options = new MobileAppConfigOptions
        {
            Versions = new MobileAppConfigVersionsOptions
            {
                Android = new MobileAppConfigPlatformVersionOptions
                {
                    MinimumBuild = -1,
                },
            },
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }
}
