using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.Api.BackgroundJobs;

public sealed class TmdbTvChangesSyncJob(ITmdbTvChangesSyncService syncService, ILogger<TmdbTvChangesSyncJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 6 * 60 * 60)]
    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.TmdbTvChanges,
            async () =>
            {
                var result = await syncService.SyncAsync(DateTime.UtcNow);

                BackgroundJobLogMessages.LogTmdbTvChangesSyncCompleted(
                    logger,
                    result.WindowsProcessed,
                    result.ChangedTmdbIdsObserved,
                    result.RelevantTargets,
                    result.Refreshed,
                    result.Skipped,
                    result.Failed,
                    result.LastCompletedEndDate);
            });
}
