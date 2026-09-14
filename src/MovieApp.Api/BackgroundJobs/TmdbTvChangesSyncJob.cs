using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.Api.BackgroundJobs;

public sealed class TmdbTvChangesSyncJob(ITmdbTvChangesSyncService syncService, ILogger<TmdbTvChangesSyncJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 6 * 60 * 60)]
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync()
    {
        var result = await syncService.SyncAsync(DateTime.UtcNow);

        logger.LogInformation(
            "TMDB TV changes sync completed: windows={WindowsProcessed} changedIds={ChangedIds} refreshedShows={RefreshedShows} lastEndDate={LastEndDate}",
            result.WindowsProcessed,
            result.ChangedTmdbIdsObserved,
            result.FollowedShowsRefreshed,
            result.LastCompletedEndDate);
    }
}
