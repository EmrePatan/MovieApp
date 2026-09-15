using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HangfireRecurringBackgroundJobRegistrar(
    IRecurringJobManager recurringJobManager,
    IOptions<BackgroundJobsOptions> backgroundJobsOptions,
    IOptions<PushNotificationsOptions> pushNotificationsOptions,
    IOptions<CatalogKeywordBackfillOptions> catalogKeywordBackfillOptions,
    IOptions<TvUpcomingEpisodeSyncOptions> tvUpcomingEpisodeSyncOptions,
    ILogger<HangfireRecurringBackgroundJobRegistrar> logger) : IRecurringBackgroundJobRegistrar
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
            BackgroundJobLogMessages.LogBackgroundJobsDisabled(logger);
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

            recurringJobManager.AddOrUpdate<TmdbMovieChangesSyncJob>(
                RecurringJobIds.TmdbMovieChanges,
                job => job.ExecuteAsync(),
                Cron.HourInterval(6),
                UtcOptions);
        }
        else
        {
            SkipRecurringJob(RecurringJobIds.TmdbTvChanges, "TmdbChangesEnabled=false");
            SkipRecurringJob(RecurringJobIds.TmdbMovieChanges, "TmdbChangesEnabled=false");
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
            SkipRecurringJob(RecurringJobIds.HotRelease, "HotReleaseEnabled=false");
        }

        if (backgroundJobs.MovieReleaseEnabled)
        {
            recurringJobManager.AddOrUpdate<MovieReleaseCheckJob>(
                RecurringJobIds.MovieRelease,
                job => job.ExecuteAsync(),
                Cron.Hourly(),
                UtcOptions);
        }
        else
        {
            SkipRecurringJob(RecurringJobIds.MovieRelease, "MovieReleaseEnabled=false");
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
            SkipRecurringJob(RecurringJobIds.ReleaseFanout, "NotificationFanoutEnabled=false");
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
            SkipRecurringJob(RecurringJobIds.PushPreparation, "PushDeliveryEnabled=false or PushNotifications:Enabled=false");
            SkipRecurringJob(RecurringJobIds.PushDispatch, "PushDeliveryEnabled=false or PushNotifications:Enabled=false");
            SkipRecurringJob(RecurringJobIds.PushReceipts, "PushDeliveryEnabled=false or PushNotifications:Enabled=false");
        }

        if (catalogKeywordBackfillOptions.Value.Enabled)
        {
            recurringJobManager.AddOrUpdate<CatalogKeywordBackfillJob>(
                RecurringJobIds.CatalogKeywordBackfill,
                job => job.ExecuteAsync(),
                catalogKeywordBackfillOptions.Value.RecurringCron,
                UtcOptions);
        }
        else
        {
            SkipRecurringJob(RecurringJobIds.CatalogKeywordBackfill, "CatalogKeywordBackfill:Enabled=false");
        }

        if (backgroundJobs.TvUpcomingEpisodeSyncEnabled && tvUpcomingEpisodeSyncOptions.Value.Enabled)
        {
            recurringJobManager.AddOrUpdate<TvUpcomingEpisodeSyncJob>(
                RecurringJobIds.TvUpcomingEpisodeSync,
                job => job.ExecuteAsync(),
                Cron.Hourly(),
                UtcOptions);
        }
        else
        {
            SkipRecurringJob(
                RecurringJobIds.TvUpcomingEpisodeSync,
                "TvUpcomingEpisodeSyncEnabled=false or TvUpcomingEpisodeSync:Enabled=false");
        }
    }

    public void RemoveAllRecurringJobs()
    {
        foreach (var jobId in RecurringJobIds.All)
        {
            recurringJobManager.RemoveIfExists(jobId);
        }
    }

    private void SkipRecurringJob(string jobId, string reason)
    {
        BackgroundJobLogMessages.LogRecurringJobRegistrationSkipped(logger, jobId, reason);
        recurringJobManager.RemoveIfExists(jobId);
    }
}
