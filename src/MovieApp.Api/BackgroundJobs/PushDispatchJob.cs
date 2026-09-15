using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.PushNotifications;

namespace MovieApp.Api.BackgroundJobs;

public sealed class PushDispatchJob(
    IPushNotificationDispatchService dispatchService,
    ILogger<PushDispatchJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.PushDispatch,
            async () =>
            {
                var result = await dispatchService.DispatchDueAsync();

                BackgroundJobLogMessages.LogPushDispatchCompleted(
                    logger,
                    result.ClaimedCount,
                    result.SentCount,
                    result.SkippedCount,
                    result.RetryableFailureCount,
                    result.PermanentFailureCount);
            });
}
