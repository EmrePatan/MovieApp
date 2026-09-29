using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class NewReleasesOptionsValidator : IValidateOptions<NewReleasesOptions>
{
    public ValidateOptionsResult Validate(string? name, NewReleasesOptions options)
    {
        if (options.MaxAgeDays < 0)
        {
            return ValidateOptionsResult.Fail(
                "NewReleases:MaxAgeDays must be greater than or equal to 0.");
        }

        return ValidateOptionsResult.Success;
    }
}
