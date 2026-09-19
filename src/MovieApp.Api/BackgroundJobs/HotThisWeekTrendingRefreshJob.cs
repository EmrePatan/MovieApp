using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.Home;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HotThisWeekTrendingRefreshJob(
    IHotThisWeekTrendingSnapshotService snapshotService,
    ILogger<HotThisWeekTrendingRefreshJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.HotThisWeekTrendingRefresh,
            async () =>
            {
                var result = await snapshotService.RefreshAsync(CancellationToken.None);

                BackgroundJobLogMessages.LogHotThisWeekTrendingRefreshCompleted(
                    logger,
                    result.SnapshotUpdated,
                    result.ProviderItemCount,
                    result.MappedItemCount,
                    result.SkippedItemCount);
            });
}
