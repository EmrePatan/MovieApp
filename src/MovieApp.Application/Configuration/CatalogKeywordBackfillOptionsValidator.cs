using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class CatalogKeywordBackfillOptionsValidator : IValidateOptions<CatalogKeywordBackfillOptions>
{
    private const int MinimumBatchSize = 1;
    private const int MaximumBatchSize = 200;
    private const int MinimumConcurrency = 1;
    private const int MaximumConcurrency = 8;

    public ValidateOptionsResult Validate(string? name, CatalogKeywordBackfillOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize < MinimumBatchSize || options.BatchSize > MaximumBatchSize)
        {
            failures.Add(
                $"CatalogKeywordBackfill:BatchSize must be between {MinimumBatchSize} and {MaximumBatchSize}.");
        }

        if (options.MaxConcurrency < MinimumConcurrency || options.MaxConcurrency > MaximumConcurrency)
        {
            failures.Add(
                $"CatalogKeywordBackfill:MaxConcurrency must be between {MinimumConcurrency} and {MaximumConcurrency}.");
        }

        if (string.IsNullOrWhiteSpace(options.RecurringCron))
        {
            failures.Add("CatalogKeywordBackfill:RecurringCron must not be empty.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
