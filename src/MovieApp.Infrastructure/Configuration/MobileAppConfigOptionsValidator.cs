using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Configuration;

public sealed class MobileAppConfigOptionsValidator : IValidateOptions<MobileAppConfigOptions>
{
    public ValidateOptionsResult Validate(string? name, MobileAppConfigOptions options)
    {
        var failures = new List<string>();

        ValidatePlatform(options.Versions.Ios, "MobileAppConfig:Versions:Ios", failures);
        ValidatePlatform(options.Versions.Android, "MobileAppConfig:Versions:Android", failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidatePlatform(
        MobileAppConfigPlatformVersionOptions platform,
        string prefix,
        List<string> failures)
    {
        if (platform.MinimumBuild < 0)
        {
            failures.Add($"{prefix}:MinimumBuild must be greater than or equal to 0.");
        }

        if (platform.LatestBuild < 0)
        {
            failures.Add($"{prefix}:LatestBuild must be greater than or equal to 0.");
        }

        if (platform.LatestBuild < platform.MinimumBuild)
        {
            failures.Add($"{prefix}:LatestBuild must be greater than or equal to MinimumBuild.");
        }

        if (!string.IsNullOrWhiteSpace(platform.StoreUrl) &&
            !Uri.TryCreate(platform.StoreUrl.Trim(), UriKind.Absolute, out _))
        {
            failures.Add($"{prefix}:StoreUrl must be a valid absolute URL when provided.");
        }
    }
}
