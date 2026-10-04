using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;
public sealed class CatalogGenreBackfillOptionsValidator : IValidateOptions<CatalogGenreBackfillOptions>
{
    private const int MinimumBatchSize = 1;
    private const int MaximumBatchSize = 200;
    private const int MinimumConcurrency = 1;
    private const int MaximumConcurrency = 4;

    public ValidateOptionsResult Validate(string? name, CatalogGenreBackfillOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize < MinimumBatchSize || options.BatchSize > MaximumBatchSize)
        {
            failures.Add(
                $"CatalogGenreBackfill:BatchSize must be between {MinimumBatchSize} and {MaximumBatchSize}.");
        }

        if (options.MaxConcurrency < MinimumConcurrency || options.MaxConcurrency > MaximumConcurrency)
        {
            failures.Add(
                $"CatalogGenreBackfill:MaxConcurrency must be between {MinimumConcurrency} and {MaximumConcurrency}.");
        }

        if (options.MaxBatchesPerRun < 1 || options.MaxBatchesPerRun > 100)
        {
            failures.Add("CatalogGenreBackfill:MaxBatchesPerRun must be between 1 and 100.");
        }

        if (options.DelayBetweenItemsMs < 0 || options.DelayBetweenItemsMs > 60_000)
        {
            failures.Add("CatalogGenreBackfill:DelayBetweenItemsMs must be between 0 and 60000.");
        }

        if (options.DelayBetweenBatchesMs < 0 || options.DelayBetweenBatchesMs > 300_000)
        {
            failures.Add("CatalogGenreBackfill:DelayBetweenBatchesMs must be between 0 and 300000.");
        }

        if (string.IsNullOrWhiteSpace(options.RecurringCron))
        {
            failures.Add("CatalogGenreBackfill:RecurringCron must not be empty.");
        }

        if (options.ProviderEmptyGenresRetryHours < 1 || options.ProviderEmptyGenresRetryHours > 720)
        {
            failures.Add("CatalogGenreBackfill:ProviderEmptyGenresRetryHours must be between 1 and 720.");
        }

        if (options.ProviderUnavailableRetryHours < 1 || options.ProviderUnavailableRetryHours > 168)
        {
            failures.Add("CatalogGenreBackfill:ProviderUnavailableRetryHours must be between 1 and 168.");
        }

        if (options.TransientFailureRetryMinutes < 1 || options.TransientFailureRetryMinutes > 1440)
        {
            failures.Add("CatalogGenreBackfill:TransientFailureRetryMinutes must be between 1 and 1440.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
