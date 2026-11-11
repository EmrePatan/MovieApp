using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.MovieChanges;

namespace MovieApp.Api.BackgroundJobs;

public sealed class TmdbMovieChangesSyncJob(
    ITmdbMovieChangesSyncService syncService,
    ILogger<TmdbMovieChangesSyncJob> logger)
{
    // Both media types use the same distributed lock; duplicate runs must not occupy workers for hours.
    [DisableConcurrentExecution("movieapp:tmdb-changes-sync", timeoutSec: 10)]
    [AutomaticRetry(Attempts = 5)]
    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.TmdbMovieChanges,
            async () =>
            {
                var result = await syncService.SyncAsync(DateTime.UtcNow, cancellationToken);

                BackgroundJobLogMessages.LogTmdbMovieChangesSyncCompleted(
                    logger,
                    result.WindowsProcessed,
                    result.ChangedTmdbIdsObserved,
                    result.ExistingUserRelevantMatches,
                    result.DiscoveryRelevantMatches,
                    result.DiscoveryOnlyRelevantMatches,
                    result.RelevantTargets,
                    result.Refreshed,
                    result.Skipped,
                    result.Failed,
                    result.LastCompletedEndDate);
            });
}
