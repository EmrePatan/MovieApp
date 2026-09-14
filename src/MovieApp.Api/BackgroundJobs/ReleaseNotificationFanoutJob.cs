using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.ReleaseNotifications;

namespace MovieApp.Api.BackgroundJobs;

public sealed class ReleaseNotificationFanoutJob(
    IReleaseNotificationFanoutRepository fanoutRepository,
    IReleaseNotificationFanoutService fanoutService,
    IOptions<BackgroundJobsOptions> options,
    ILogger<ReleaseNotificationFanoutJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 5 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync()
    {
        var eventIds = await fanoutRepository.GetPendingFanoutEventIdsAsync(
            options.Value.FanoutBatchSize);

        if (eventIds.Count == 0)
        {
            logger.LogInformation("Release notification fanout completed: no pending events");
            return;
        }

        var result = await fanoutService.ProcessAsync(eventIds);

        logger.LogInformation(
            "Release notification fanout completed: events={EventsProcessed} discovered={Discovered} notifications={NotificationsCreated} links={LinksCreated} skippedPreference={SkippedPreference} skippedBoundary={SkippedBoundary} skippedSource={SkippedSource}",
            result.EventsProcessed,
            eventIds.Count,
            result.NotificationsCreated,
            result.EventLinksCreated,
            result.SkippedByPreference,
            result.SkippedByBoundary,
            result.SkippedBySource);
    }
}
