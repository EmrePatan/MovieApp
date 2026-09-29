using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class AdvancedDiscoverOptionsValidator : IValidateOptions<AdvancedDiscoverOptions>
{
    public ValidateOptionsResult Validate(string? name, AdvancedDiscoverOptions options)
    {
        var failures = new List<string>();

        if (options.MinVoteCountWhenWatchProvider < 0)
        {
            failures.Add(
                "AdvancedDiscover:MinVoteCountWhenWatchProvider must be greater than or equal to 0.");
        }

        if (options.TransactionalWatchProviderIds is null)
        {
            failures.Add("AdvancedDiscover:TransactionalWatchProviderIds must not be null.");
        }
        else if (options.TransactionalWatchProviderIds.Any(providerId => providerId < 1))
        {
            failures.Add(
                "AdvancedDiscover:TransactionalWatchProviderIds entries must be greater than or equal to 1.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
