using Hangfire;
using Microsoft.Extensions.Options;
using MovieApp.Api.BackgroundJobs;
using MovieApp.Application.Configuration;

namespace MovieApp.UnitTests.BackgroundJobs;

public sealed class HangfireRecurringBackgroundJobRegistrarTests
{
    [Fact]
    public void RegisterRecurringJobs_UsesStableJobIdsAndUtcCadences()
    {
        var manager = new FakeRecurringJobManager();
        var registrar = CreateRegistrar(
            manager,
            enabled: true,
            pushEnabled: true);

        registrar.RegisterRecurringJobs();

        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.TmdbTvChanges && entry.Cron == Cron.HourInterval(6));
        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.HotRelease && entry.Cron == Cron.Hourly());
        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.MovieRelease && entry.Cron == Cron.Hourly());
        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.ReleaseFanout && entry.Cron == Cron.MinuteInterval(5));
        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.PushPreparation && entry.Cron == Cron.MinuteInterval(5));
        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.PushDispatch && entry.Cron == Cron.MinuteInterval(5));
        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.PushReceipts && entry.Cron == Cron.MinuteInterval(15));
    }

    [Fact]
    public void RegisterRecurringJobs_WhenDisabled_RemovesAllJobs()
    {
        var manager = new FakeRecurringJobManager();
        var registrar = CreateRegistrar(manager, enabled: false, pushEnabled: true);

        registrar.RegisterRecurringJobs();

        Assert.Equal(RecurringJobIds.All.Count, manager.Removed.Count);
        Assert.Empty(manager.AddedOrUpdated);
    }

    [Fact]
    public void RegisterRecurringJobs_WhenPushNotificationsDisabled_OmitsPushJobs()
    {
        var manager = new FakeRecurringJobManager();
        var registrar = CreateRegistrar(manager, enabled: true, pushEnabled: false);

        registrar.RegisterRecurringJobs();

        Assert.DoesNotContain(manager.AddedOrUpdated, entry => entry.JobId == RecurringJobIds.PushPreparation);
        Assert.DoesNotContain(manager.AddedOrUpdated, entry => entry.JobId == RecurringJobIds.PushDispatch);
        Assert.DoesNotContain(manager.AddedOrUpdated, entry => entry.JobId == RecurringJobIds.PushReceipts);
        Assert.Contains(RecurringJobIds.PushPreparation, manager.Removed);
    }

    [Fact]
    public void RegisterRecurringJobs_IsIdempotent()
    {
        var manager = new FakeRecurringJobManager();
        var registrar = CreateRegistrar(manager, enabled: true, pushEnabled: true);

        registrar.RegisterRecurringJobs();
        registrar.RegisterRecurringJobs();

        Assert.Equal(7, manager.AddedOrUpdated.Count);
    }

    [Fact]
    public void RegisterRecurringJobs_WhenKeywordBackfillEnabled_RegistersCatalogKeywordBackfillJob()
    {
        var manager = new FakeRecurringJobManager();
        var registrar = CreateRegistrar(
            manager,
            enabled: true,
            pushEnabled: true,
            keywordBackfillEnabled: true);

        registrar.RegisterRecurringJobs();

        Assert.Contains(
            manager.AddedOrUpdated,
            entry => entry.JobId == RecurringJobIds.CatalogKeywordBackfill &&
                     entry.Cron == "0 * * * *");
    }

    [Fact]
    public void RegisterRecurringJobs_WhenKeywordBackfillDisabled_RemovesCatalogKeywordBackfillJob()
    {
        var manager = new FakeRecurringJobManager();
        var registrar = CreateRegistrar(
            manager,
            enabled: true,
            pushEnabled: true,
            keywordBackfillEnabled: false);

        registrar.RegisterRecurringJobs();

        Assert.DoesNotContain(manager.AddedOrUpdated, entry => entry.JobId == RecurringJobIds.CatalogKeywordBackfill);
        Assert.Contains(RecurringJobIds.CatalogKeywordBackfill, manager.Removed);
    }

    private static HangfireRecurringBackgroundJobRegistrar CreateRegistrar(
        FakeRecurringJobManager manager,
        bool enabled,
        bool pushEnabled,
        bool keywordBackfillEnabled = false) =>
        new(
            manager,
            Options.Create(new BackgroundJobsOptions
            {
                Enabled = enabled,
                TmdbChangesEnabled = true,
                HotReleaseEnabled = true,
                NotificationFanoutEnabled = true,
                PushDeliveryEnabled = true
            }),
            Options.Create(new PushNotificationsOptions
            {
                Enabled = pushEnabled
            }),
            Options.Create(new CatalogKeywordBackfillOptions
            {
                Enabled = keywordBackfillEnabled,
                RecurringCron = "0 * * * *"
            }));
}
