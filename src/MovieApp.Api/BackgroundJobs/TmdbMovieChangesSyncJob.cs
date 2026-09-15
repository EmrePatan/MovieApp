using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.MovieChanges;

namespace MovieApp.Api.BackgroundJobs;

public sealed class TmdbMovieChangesSyncJob(
    ITmdbMovieChangesSyncService syncService,
    ILogger<TmdbMovieChangesSyncJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 6 * 60 * 60)]
    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.TmdbMovieChanges,
            async () =>
            {
                var result = await syncService.SyncAsync(DateTime.UtcNow);

                BackgroundJobLogMessages.LogTmdbMovieChangesSyncCompleted(
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
