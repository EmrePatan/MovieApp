using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class InsightsV3OptionsValidator : IValidateOptions<InsightsV3Options>
{
    public ValidateOptionsResult Validate(string? name, InsightsV3Options options)
    {
        var failures = new List<string>();

        if (options.MaxRepositoryConcurrency < 1 || options.MaxRepositoryConcurrency > InsightsV3Options.V3RepositoryPhaseCount)
        {
            failures.Add(
                $"{InsightsV3Options.SectionName}:MaxRepositoryConcurrency must be between 1 and {InsightsV3Options.V3RepositoryPhaseCount}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
