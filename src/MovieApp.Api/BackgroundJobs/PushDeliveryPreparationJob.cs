using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.PushNotifications;

namespace MovieApp.Api.BackgroundJobs;

public sealed class PushDeliveryPreparationJob(
    IPushNotificationDeliveryRepository deliveryRepository,
    IPushNotificationDeliveryPreparationService preparationService,
    IOptions<BackgroundJobsOptions> options,
    ILogger<PushDeliveryPreparationJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 5 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync()
    {
        var notificationIds = await deliveryRepository.GetNotificationIdsNeedingPreparationAsync(
            options.Value.PreparationBatchSize);

        if (notificationIds.Count == 0)
        {
            BackgroundJobLogMessages.LogPushDeliveryPreparationNoPendingNotifications(logger);
            return;
        }

        var result = await preparationService.PrepareAsync(notificationIds);

        BackgroundJobLogMessages.LogPushDeliveryPreparationCompleted(
            logger,
            notificationIds.Count,
            result.NotificationsProcessed,
            result.DeliveriesCreated);
    }
}
