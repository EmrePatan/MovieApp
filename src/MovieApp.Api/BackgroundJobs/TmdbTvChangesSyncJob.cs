using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.Api.BackgroundJobs;

public sealed class TmdbTvChangesSyncJob(ITmdbTvChangesSyncService syncService, ILogger<TmdbTvChangesSyncJob> logger)
{
    // Shares the movie sync lock so movie and TV refreshes cannot overlap.
    [DisableConcurrentExecution("movieapp:tmdb-changes-sync", timeoutSec: 10)]
    [AutomaticRetry(Attempts = 5)]
    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.TmdbTvChanges,
            async () =>
            {
                var result = await syncService.SyncAsync(DateTime.UtcNow, cancellationToken);

                BackgroundJobLogMessages.LogTmdbTvChangesSyncCompleted(
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
