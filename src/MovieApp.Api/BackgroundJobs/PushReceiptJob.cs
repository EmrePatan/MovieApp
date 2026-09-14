using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.PushNotifications;

namespace MovieApp.Api.BackgroundJobs;

public sealed class PushReceiptJob(
    IPushNotificationReceiptService receiptService,
    ILogger<PushReceiptJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync()
    {
        var result = await receiptService.ProcessReceiptsAsync();

        BackgroundJobLogMessages.LogPushReceiptProcessingCompleted(
            logger,
            result.ProcessedCount,
            result.DeliveredCount,
            result.RetryableFailureCount,
            result.PermanentFailureCount);
    }
}
