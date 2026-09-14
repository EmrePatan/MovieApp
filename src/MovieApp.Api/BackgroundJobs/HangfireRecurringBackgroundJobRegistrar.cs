using Hangfire;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HangfireRecurringBackgroundJobRegistrar(
    IRecurringJobManager recurringJobManager,
    IOptions<BackgroundJobsOptions> backgroundJobsOptions,
    IOptions<PushNotificationsOptions> pushNotificationsOptions) : IRecurringBackgroundJobRegistrar
{
    private static readonly RecurringJobOptions UtcOptions = new()
    {
        TimeZone = TimeZoneInfo.Utc
    };

    public void RegisterRecurringJobs()
    {
        var backgroundJobs = backgroundJobsOptions.Value;
        var pushNotifications = pushNotificationsOptions.Value;

        if (!backgroundJobs.Enabled)
        {
            RemoveAllRecurringJobs();
            return;
        }

        if (backgroundJobs.TmdbChangesEnabled)
        {
            recurringJobManager.AddOrUpdate<TmdbTvChangesSyncJob>(
                RecurringJobIds.TmdbTvChanges,
                job => job.ExecuteAsync(),
                Cron.HourInterval(6),
                UtcOptions);
        }
        else
        {
            recurringJobManager.RemoveIfExists(RecurringJobIds.TmdbTvChanges);
        }

        if (backgroundJobs.HotReleaseEnabled)
        {
            recurringJobManager.AddOrUpdate<HotReleaseCheckJob>(
                RecurringJobIds.HotRelease,
                job => job.ExecuteAsync(),
                Cron.Hourly(),
                UtcOptions);
        }
        else
        {
            recurringJobManager.RemoveIfExists(RecurringJobIds.HotRelease);
        }

        if (backgroundJobs.NotificationFanoutEnabled)
        {
            recurringJobManager.AddOrUpdate<ReleaseNotificationFanoutJob>(
                RecurringJobIds.ReleaseFanout,
                job => job.ExecuteAsync(),
                Cron.MinuteInterval(5),
                UtcOptions);
        }
        else
        {
            recurringJobManager.RemoveIfExists(RecurringJobIds.ReleaseFanout);
        }

        var pushJobsEnabled = backgroundJobs.PushDeliveryEnabled && pushNotifications.Enabled;

        if (pushJobsEnabled)
        {
            recurringJobManager.AddOrUpdate<PushDeliveryPreparationJob>(
                RecurringJobIds.PushPreparation,
                job => job.ExecuteAsync(),
                Cron.MinuteInterval(5),
                UtcOptions);

            recurringJobManager.AddOrUpdate<PushDispatchJob>(
                RecurringJobIds.PushDispatch,
                job => job.ExecuteAsync(),
                Cron.MinuteInterval(5),
                UtcOptions);

            recurringJobManager.AddOrUpdate<PushReceiptJob>(
                RecurringJobIds.PushReceipts,
                job => job.ExecuteAsync(),
                Cron.MinuteInterval(15),
                UtcOptions);
        }
        else
        {
            recurringJobManager.RemoveIfExists(RecurringJobIds.PushPreparation);
            recurringJobManager.RemoveIfExists(RecurringJobIds.PushDispatch);
            recurringJobManager.RemoveIfExists(RecurringJobIds.PushReceipts);
        }
    }

    public void RemoveAllRecurringJobs()
    {
        foreach (var jobId in RecurringJobIds.All)
        {
            recurringJobManager.RemoveIfExists(jobId);
        }
    }
}
