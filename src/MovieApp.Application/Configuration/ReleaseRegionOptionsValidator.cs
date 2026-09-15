using Microsoft.Extensions.Options;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Configuration;

public sealed class ReleaseRegionOptionsValidator : IValidateOptions<ReleaseRegionOptions>
{
    public ValidateOptionsResult Validate(string? name, ReleaseRegionOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DefaultRegion))
        {
            failures.Add("ReleaseRegion:DefaultRegion must not be empty.");
        }
        else
        {
            var normalized = WatchProviderRegionValidator.Normalize(options.DefaultRegion);
            var validation = WatchProviderRegionValidator.Validate(normalized);
            if (!validation.IsValid)
            {
                failures.Add($"ReleaseRegion:DefaultRegion {validation.ErrorMessage}");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
