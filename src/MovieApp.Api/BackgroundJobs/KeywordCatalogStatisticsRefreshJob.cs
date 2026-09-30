using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Keywords;

namespace MovieApp.Api.BackgroundJobs;

public sealed class KeywordCatalogStatisticsRefreshJob(
    IKeywordCatalogStatisticsRefreshService refreshService,
    ILogger<KeywordCatalogStatisticsRefreshJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    [AutomaticRetry(Attempts = 1)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.KeywordCatalogStatisticsRefresh,
            async () =>
            {
                var result = await refreshService.RefreshAsync(CancellationToken.None);
                if (!result.Succeeded)
                {
                    BackgroundJobLogMessages.LogKeywordCatalogStatisticsRefreshFailed(
                        logger,
                        result.FailureReason ?? "unknown",
                        result.DurationMilliseconds);
                }
            });
}
