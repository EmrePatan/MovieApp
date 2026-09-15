using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.TvUpcomingEpisodes;

namespace MovieApp.Api.BackgroundJobs;

public sealed class TvUpcomingEpisodeSyncJob(
    ITvUpcomingEpisodeSyncService syncService,
    ILogger<TvUpcomingEpisodeSyncJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 1)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.TvUpcomingEpisodeSync,
            async () =>
            {
                var result = await syncService.RunAsync();

                BackgroundJobLogMessages.LogTvUpcomingEpisodeSyncCompleted(
                    logger,
                    result.Selected,
                    result.Succeeded,
                    result.Failed,
                    result.Hydrated);
            });
}
