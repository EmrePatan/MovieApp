using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class TopRatedOptionsValidator : IValidateOptions<TopRatedOptions>
{
    public ValidateOptionsResult Validate(string? name, TopRatedOptions options)
    {
        var failures = new List<string>();

        if (options.MinimumVoteConfidence < 0)
        {
            failures.Add("TopRated:MinimumVoteConfidence must be greater than or equal to 0.");
        }

        if (options.MinVoteCountMovie < 0)
        {
            failures.Add("TopRated:MinVoteCountMovie must be greater than or equal to 0.");
        }

        if (options.MinVoteCountTv < 0)
        {
            failures.Add("TopRated:MinVoteCountTv must be greater than or equal to 0.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
