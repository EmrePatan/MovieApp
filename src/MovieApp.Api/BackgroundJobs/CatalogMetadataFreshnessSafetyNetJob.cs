using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.Catalog;

namespace MovieApp.Api.BackgroundJobs;

public sealed class CatalogMetadataFreshnessSafetyNetJob(
    ICatalogMetadataFreshnessSafetyNetService safetyNetService,
    ICatalogMetadataFreshnessDiagnosticsService diagnosticsService,
    ILogger<CatalogMetadataFreshnessSafetyNetJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 6 * 60 * 60)]
    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.CatalogMetadataFreshnessSafetyNet,
            async () =>
            {
                var result = await safetyNetService.RunAsync();

                BackgroundJobLogMessages.LogCatalogMetadataFreshnessSafetyNetCompleted(
                    logger,
                    result.EligibleStaleCount,
                    result.SelectedBatchCount,
                    result.MoviesSelected,
                    result.TvShowsSelected,
                    result.Refreshed,
                    result.Skipped,
                    result.Failed,
                    result.RemainingStaleEstimate,
                    result.DurationMs);

                var distribution = await diagnosticsService.GetDiscoveryFreshnessDistributionAsync();
                BackgroundJobLogMessages.LogCatalogMetadataFreshnessDistribution(
                    logger,
                    distribution.DiscoveryRelevantTotal,
                    distribution.FreshWithin24Hours,
                    distribution.Fresh24To72Hours,
                    distribution.StaleOver72Hours,
                    distribution.StaleOver7Days,
                    distribution.NeverRefreshed,
                    distribution.FreshWithin72HoursPercent);
            });
}
