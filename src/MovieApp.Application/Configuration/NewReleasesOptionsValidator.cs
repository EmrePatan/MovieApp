using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class NewReleasesOptionsValidator : IValidateOptions<NewReleasesOptions>
{
    public ValidateOptionsResult Validate(string? name, NewReleasesOptions options)
    {
        var failures = new List<string>();

        if (options.MaxAgeDays < 0)
        {
            failures.Add("NewReleases:MaxAgeDays must be greater than or equal to 0.");
        }

        if (options.MinVoteCountMovie < 0)
        {
            failures.Add("NewReleases:MinVoteCountMovie must be greater than or equal to 0.");
        }

        if (options.MinVoteCountTv < 0)
        {
            failures.Add("NewReleases:MinVoteCountTv must be greater than or equal to 0.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
