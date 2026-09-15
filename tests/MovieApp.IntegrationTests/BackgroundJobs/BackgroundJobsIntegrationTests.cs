using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Api.BackgroundJobs;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;
using MovieApp.Domain.Users;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.PushNotifications;
using MovieApp.IntegrationTests.ReleaseNotifications;

namespace MovieApp.IntegrationTests.BackgroundJobs;

[CollectionDefinition("BackgroundJobs")]
public sealed class BackgroundJobsTestsDefinition : ICollectionFixture<BackgroundJobsFixture>;

[Collection("BackgroundJobs")]
public sealed class BackgroundJobsIntegrationTests(BackgroundJobsFixture fixture)
{
    [Fact]
    public void DisabledBackgroundJobsDoNotRegisterRecurringJobs()
    {
        var manager = new RecordingRecurringJobManager();
        var registrar = CreateRegistrar(
            manager,
            enabled: false,
            pushEnabled: true);

        registrar.RegisterRecurringJobs();

        Assert.Equal(RecurringJobIds.All.Count, manager.Removed.Count);
        Assert.Empty(manager.AddedOrUpdated);
    }

    [Fact]
    public void PushDisabledConfigurationOmitsPushJobs()
    {
        var manager = new RecordingRecurringJobManager();
        var registrar = CreateRegistrar(
            manager,
            enabled: true,
            pushEnabled: false);

        registrar.RegisterRecurringJobs();

        Assert.DoesNotContain(
            manager.AddedOrUpdated,
            entry => entry.JobId.StartsWith("movieapp:push-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FanoutDiscoveryExcludesBaselineAbsorbAndIsBounded()
    {
        await BackgroundJobsFixture.ResetFanoutAsync();
        await SeedFanoutDiscoveryDataAsync();

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        var repository = new ReleaseNotificationFanoutRepository(context, new CatalogFollowRepository(context));

        var pending = await repository.GetPendingFanoutEventIdsAsync(1);

        Assert.Single(pending);
        var onlyEvent = await context.CatalogReleaseEvents.SingleAsync(e => e.Id == pending[0]);
        Assert.Equal(CatalogReleaseEventSource.BoundaryDetection, onlyEvent.Source);
    }

    [Fact]
    public async Task PreparationDiscoveryIsBounded()
    {
        await fixture.ResetPushAsync();
        await SeedPreparationDiscoveryDataAsync(deviceCount: 3);

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var repository = new PushNotificationDeliveryRepository(context);

        var pending = await repository.GetNotificationIdsNeedingPreparationAsync(1);

        Assert.Single(pending);
    }

    [Fact]
    public async Task FanoutJobReprocessingRemainsIdempotent()
    {
        await BackgroundJobsFixture.ResetFanoutAsync();
        await SeedSingleEligibleFanoutEventAsync();

        using var scope = fixture.FanoutFactory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<ReleaseNotificationFanoutJob>();

        await job.ExecuteAsync();
        await job.ExecuteAsync();

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        Assert.Equal(1, await context.UserReleaseNotifications.CountAsync());
        Assert.Equal(1, await context.UserReleaseNotificationEvents.CountAsync());
    }

    [Fact]
    public async Task PreparationJobReprocessingRemainsIdempotent()
    {
        await fixture.ResetPushAsync();
        await SeedSingleNotificationWithDeviceAsync();

        using var scope = fixture.PushFactory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PushDeliveryPreparationJob>();

        await job.ExecuteAsync();
        await job.ExecuteAsync();

        await using var context = PushNotificationDeliveryFixture.CreateContext();
        Assert.Equal(1, await context.PushNotificationDeliveries.CountAsync());
    }

    private static HangfireRecurringBackgroundJobRegistrar CreateRegistrar(
        RecordingRecurringJobManager manager,
        bool enabled,
        bool pushEnabled,
        bool keywordBackfillEnabled = false,
        bool tvUpcomingEpisodeSyncEnabled = false) =>
        new(
            manager,
            Options.Create(new BackgroundJobsOptions
            {
                Enabled = enabled,
                TmdbChangesEnabled = true,
                HotReleaseEnabled = true,
                TvUpcomingEpisodeSyncEnabled = tvUpcomingEpisodeSyncEnabled,
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
            }),
            Options.Create(new TvUpcomingEpisodeSyncOptions
            {
                Enabled = tvUpcomingEpisodeSyncEnabled
            }));

    private static async Task SeedFanoutDiscoveryDataAsync()
    {
        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        var user = User.Create(Guid.NewGuid(), "discovery@example.com", "hash", "Discovery", DateTime.UtcNow);
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.TvShows.Add(tvShow);
        var follow = CatalogFollow.CreateTvFollow(user.Id, tvShow.Id, true, true, DateTime.UtcNow);
        follow.SetNotifyFromUtc(DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        context.CatalogFollows.Add(follow);
        context.CatalogReleaseEvents.Add(CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShow.Id,
            1,
            1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            CatalogReleaseEventSource.BaselineAbsorb,
            DateTime.UtcNow));
        context.CatalogReleaseEvents.Add(CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShow.Id,
            1,
            2,
            DateOnly.FromDateTime(DateTime.UtcNow),
            CatalogReleaseEventSource.BoundaryDetection,
            DateTime.UtcNow));
        context.CatalogReleaseEvents.Add(CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShow.Id,
            1,
            3,
            DateOnly.FromDateTime(DateTime.UtcNow),
            CatalogReleaseEventSource.BoundaryDetection,
            DateTime.UtcNow));
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedSingleEligibleFanoutEventAsync()
    {
        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        var user = User.Create(Guid.NewGuid(), "job-fanout@example.com", "hash", "Job Fanout", DateTime.UtcNow);
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            Title = "Job Fanout",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.TvShows.Add(tvShow);
        var follow = CatalogFollow.CreateTvFollow(user.Id, tvShow.Id, true, true, DateTime.UtcNow);
        follow.SetNotifyFromUtc(DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        context.CatalogFollows.Add(follow);
        var releaseEvent = CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShow.Id,
            1,
            1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            CatalogReleaseEventSource.BoundaryDetection,
            DateTime.UtcNow);
        context.CatalogReleaseEvents.Add(releaseEvent);
        await context.SaveChangesAsync();
        return releaseEvent.Id;
    }

    private static async Task SeedPreparationDiscoveryDataAsync(int deviceCount)
    {
        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var user = User.Create(Guid.NewGuid(), "prep@example.com", "hash", "Prep", DateTime.UtcNow);
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            Title = "Prep",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.TvShows.Add(tvShow);
        context.UserReleaseNotifications.Add(new UserReleaseNotification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TvShowId = tvShow.Id,
            NotificationType = UserReleaseNotificationType.NewEpisodes,
            Status = UserReleaseNotificationStatus.Pending,
            AggregationWindowKey = "2026-09-15",
            CreatedAtUtc = DateTime.UtcNow
        });
        for (var index = 0; index < deviceCount; index++)
        {
            context.PushDevices.Add(new PushDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ExpoPushToken = $"ExponentPushToken[dddddddddddddddddddddddddd{index:D2}]",
                Platform = PushDevicePlatform.Android,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedSingleNotificationWithDeviceAsync()
    {
        await using var context = PushNotificationDeliveryFixture.CreateContext();
        var user = User.Create(Guid.NewGuid(), "prep-job@example.com", "hash", "Prep Job", DateTime.UtcNow);
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            Title = "Prep Job",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var notificationId = Guid.NewGuid();
        context.Users.Add(user);
        context.TvShows.Add(tvShow);
        context.UserReleaseNotifications.Add(new UserReleaseNotification
        {
            Id = notificationId,
            UserId = user.Id,
            TvShowId = tvShow.Id,
            NotificationType = UserReleaseNotificationType.NewEpisodes,
            Status = UserReleaseNotificationStatus.Pending,
            AggregationWindowKey = "2026-09-15",
            CreatedAtUtc = DateTime.UtcNow
        });
        context.PushDevices.Add(new PushDevice
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ExpoPushToken = "ExponentPushToken[eeeeeeeeeeeeeeeeeeeeeeeeeeeeee]",
            Platform = PushDevicePlatform.Android,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return notificationId;
    }

    private sealed class RecordingRecurringJobManager : IRecurringJobManager
    {
        public List<(string JobId, string Cron)> AddedOrUpdated { get; } = [];

        public List<string> Removed { get; } = [];

        public void AddOrUpdate(string recurringJobId, Hangfire.Common.Job job, string cronExpression, RecurringJobOptions? options = null) =>
            AddedOrUpdated.Add((recurringJobId, cronExpression));

        public void AddOrUpdate<T>(string recurringJobId, System.Linq.Expressions.Expression<Action<T>> methodCall, string cronExpression) =>
            AddedOrUpdated.Add((recurringJobId, cronExpression));

        public void AddOrUpdate<T>(string recurringJobId, System.Linq.Expressions.Expression<Action<T>> methodCall, string cronExpression, RecurringJobOptions options) =>
            AddedOrUpdated.Add((recurringJobId, cronExpression));

        public void AddOrUpdate<T>(string recurringJobId, System.Linq.Expressions.Expression<Func<T, Task>> methodCall, string cronExpression) =>
            AddedOrUpdated.Add((recurringJobId, cronExpression));

        public void AddOrUpdate<T>(string recurringJobId, System.Linq.Expressions.Expression<Func<T, Task>> methodCall, string cronExpression, RecurringJobOptions options) =>
            AddedOrUpdated.Add((recurringJobId, cronExpression));

        public void RemoveIfExists(string recurringJobId) => Removed.Add(recurringJobId);

        public void Trigger(string recurringJobId) => throw new NotSupportedException();

        public void TriggerJob(string recurringJobId) => throw new NotSupportedException();
    }
}
