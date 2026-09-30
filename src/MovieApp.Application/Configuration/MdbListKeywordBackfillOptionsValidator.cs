using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class MdbListKeywordBackfillOptionsValidator : IValidateOptions<MdbListKeywordBackfillOptions>
{
    private const int MinimumBatchSize = 1;
    private const int MaximumBatchSize = 200;
    private const int MinimumConcurrency = 1;
    private const int MaximumConcurrency = 8;

    public ValidateOptionsResult Validate(string? name, MdbListKeywordBackfillOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize < MinimumBatchSize || options.BatchSize > MaximumBatchSize)
        {
            failures.Add(
                $"MdbListKeywordBackfill:BatchSize must be between {MinimumBatchSize} and {MaximumBatchSize}.");
        }

        if (options.MaxConcurrency < MinimumConcurrency || options.MaxConcurrency > MaximumConcurrency)
        {
            failures.Add(
                $"MdbListKeywordBackfill:MaxConcurrency must be between {MinimumConcurrency} and {MaximumConcurrency}.");
        }

        if (options.DelayBetweenItemsMs < 0 || options.DelayBetweenItemsMs > 60_000)
        {
            failures.Add("MdbListKeywordBackfill:DelayBetweenItemsMs must be between 0 and 60000.");
        }

        if (string.IsNullOrWhiteSpace(options.RecurringCron))
        {
            failures.Add("MdbListKeywordBackfill:RecurringCron must not be empty.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
