using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.PushNotifications;

namespace MovieApp.Api.BackgroundJobs;

public sealed class PushDispatchJob(
    IPushNotificationDispatchService dispatchService,
    ILogger<PushDispatchJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync()
    {
        var result = await dispatchService.DispatchDueAsync();

        logger.LogInformation(
            "Push dispatch completed: claimed={Claimed} sent={Sent} skipped={Skipped} retryableFailures={RetryableFailures} permanentFailures={PermanentFailures}",
            result.ClaimedCount,
            result.SentCount,
            result.SkippedCount,
            result.RetryableFailureCount,
            result.PermanentFailureCount);
    }
}
