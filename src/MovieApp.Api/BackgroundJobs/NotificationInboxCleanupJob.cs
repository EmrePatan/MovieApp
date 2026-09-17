using System.Diagnostics;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Notifications;

namespace MovieApp.Api.BackgroundJobs;

public sealed class NotificationInboxCleanupJob(
    INotificationInboxCleanupService cleanupService,
    IOptions<NotificationRetentionOptions> options,
    ILogger<NotificationInboxCleanupJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.NotificationInboxCleanup,
            async () =>
            {
                var cutoffUtc = NotificationInboxRetention.GetReadExpirationCutoffUtc(
                    DateTime.UtcNow,
                    options.Value.ReadRetentionDays);
                var stopwatch = Stopwatch.StartNew();
                var deletedCount = await cleanupService.CleanupExpiredReadNotificationsAsync(CancellationToken.None);
                stopwatch.Stop();

                BackgroundJobLogMessages.LogNotificationInboxCleanupCompleted(
                    logger,
                    deletedCount,
                    cutoffUtc,
                    stopwatch.ElapsedMilliseconds);
            });
}
