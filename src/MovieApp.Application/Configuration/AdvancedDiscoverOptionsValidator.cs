using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class AdvancedDiscoverOptionsValidator : IValidateOptions<AdvancedDiscoverOptions>
{
    public ValidateOptionsResult Validate(string? name, AdvancedDiscoverOptions options)
    {
        if (options.MinVoteCountWhenWatchProvider < 0)
        {
            return ValidateOptionsResult.Fail(
                "AdvancedDiscover:MinVoteCountWhenWatchProvider must be greater than or equal to 0.");
        }

        return ValidateOptionsResult.Success;
    }
}
