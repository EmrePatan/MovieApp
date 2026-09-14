using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Services.ReleaseNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.IntegrationTests.ReleaseNotifications;

[CollectionDefinition("ReleaseNotificationFanout")]
public sealed class ReleaseNotificationFanoutCollection : ICollectionFixture<ReleaseNotificationFanoutFixture>;

[Collection("ReleaseNotificationFanout")]
public sealed class ReleaseNotificationFanoutIntegrationTests(ReleaseNotificationFanoutFixture fixture)
{
    private static readonly DateOnly ReleaseDate = new(2026, 9, 15);

    [Fact]
    public async Task EpisodeEventFansOutToEligibleFollower()
    {
        await fixture.ResetAsync();

        var (tvShowId, userId, releaseEventId) = await SeedFollowedShowWithEventAsync();

        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IReleaseNotificationFanoutService>()
            .ProcessAsync([releaseEventId]);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(1, result.EventLinksCreated);

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        var notification = await context.UserReleaseNotifications.SingleAsync();
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(tvShowId, notification.TvShowId);
        Assert.Equal(UserReleaseNotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public async Task PreferencesAreRespected()
    {
        await fixture.ResetAsync();

        var (_, _, releaseEventId) = await SeedFollowedShowWithEventAsync(notifyNewEpisodes: false);

        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IReleaseNotificationFanoutService>()
            .ProcessAsync([releaseEventId]);

        Assert.Equal(0, result.NotificationsCreated);
        Assert.True(result.SkippedByPreference > 0);
    }

    [Fact]
    public async Task BaselineEventsNeverFanOut()
    {
        await fixture.ResetAsync();

        var (_, _, releaseEventId) = await SeedFollowedShowWithEventAsync(
            source: CatalogReleaseEventSource.BaselineAbsorb);

        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IReleaseNotificationFanoutService>()
            .ProcessAsync([releaseEventId]);

        Assert.Equal(0, result.NotificationsCreated);
        Assert.Equal(1, result.SkippedBySource);

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        Assert.Equal(0, await context.UserReleaseNotifications.CountAsync());
    }

    [Fact]
    public async Task SameDayEpisodesAggregateIntoOneNotification()
    {
        await fixture.ResetAsync();

        var (tvShowId, userId, firstEventId, secondEventId) = await SeedFollowedShowWithTwoEpisodeEventsAsync();

        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IReleaseNotificationFanoutService>()
            .ProcessAsync([firstEventId, secondEventId]);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(2, result.EventLinksCreated);

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        Assert.Equal(1, await context.UserReleaseNotifications.CountAsync());
        Assert.Equal(2, await context.UserReleaseNotificationEvents.CountAsync());
        var notification = await context.UserReleaseNotifications.SingleAsync();
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(tvShowId, notification.TvShowId);
        Assert.Contains("2 new episodes", notification.Body);
    }

    [Fact]
    public async Task MultipleFollowersReceiveIndependentNotifications()
    {
        await fixture.ResetAsync();

        var (tvShowId, userAId, userBId, releaseEventId) = await SeedShowWithTwoFollowersAsync();

        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IReleaseNotificationFanoutService>()
            .ProcessAsync([releaseEventId]);

        Assert.Equal(2, result.NotificationsCreated);
        Assert.Equal(2, result.EventLinksCreated);

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        Assert.Equal(2, await context.UserReleaseNotifications.CountAsync());
        Assert.All(
            await context.UserReleaseNotifications.ToListAsync(),
            notification =>
            {
                Assert.Equal(tvShowId, notification.TvShowId);
                Assert.Equal(UserReleaseNotificationType.NewEpisodes, notification.NotificationType);
            });
    }

    [Fact]
    public async Task ReprocessingIsIdempotent()
    {
        await fixture.ResetAsync();

        var (_, _, releaseEventId) = await SeedFollowedShowWithEventAsync();

        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReleaseNotificationFanoutService>();

        await service.ProcessAsync([releaseEventId]);
        var second = await service.ProcessAsync([releaseEventId]);

        Assert.Equal(0, second.NotificationsCreated);
        Assert.Equal(0, second.EventLinksCreated);

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        Assert.Equal(1, await context.UserReleaseNotifications.CountAsync());
        Assert.Equal(1, await context.UserReleaseNotificationEvents.CountAsync());
    }

    [Fact]
    public async Task ConcurrentFanoutDoesNotDuplicateNotifications()
    {
        await fixture.ResetAsync();

        var (_, _, releaseEventId) = await SeedFollowedShowWithEventAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IReleaseNotificationFanoutService>();
            return await service.ProcessAsync([releaseEventId]);
        }));

        Assert.Contains(results, result => result.NotificationsCreated > 0);

        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        Assert.Equal(1, await context.UserReleaseNotifications.CountAsync());
        Assert.Equal(1, await context.UserReleaseNotificationEvents.CountAsync());
    }

    [Fact]
    public async Task NotificationBoundaryUsesDateSemantics()
    {
        await fixture.ResetAsync();

        var (_, _, releaseEventId) = await SeedFollowedShowWithEventAsync(
            notifyFromUtc: new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc));

        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IReleaseNotificationFanoutService>()
            .ProcessAsync([releaseEventId]);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(0, result.SkippedByBoundary);
    }

    [Fact]
    public async Task JunctionUserIntegrityStillHolds()
    {
        await fixture.ResetAsync();

        Guid userAId;
        Guid userBId;
        Guid notificationId;
        Guid releaseEventId;

        await using (var seedContext = ReleaseNotificationFanoutFixture.CreateContext())
        {
            var userA = User.Create(Guid.NewGuid(), "junction-a@example.com", "hash", "User A", DateTime.UtcNow);
            var userB = User.Create(Guid.NewGuid(), "junction-b@example.com", "hash", "User B", DateTime.UtcNow);
            var tvShow = new TvShow
            {
                Id = Guid.NewGuid(),
                Title = "Junction Show",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            seedContext.Users.AddRange(userA, userB);
            seedContext.TvShows.Add(tvShow);

            var releaseEvent = CatalogReleaseEventFactory.CreateEpisodeEvent(
                tvShow.Id,
                1,
                1,
                ReleaseDate,
                CatalogReleaseEventSource.BoundaryDetection,
                DateTime.UtcNow);
            seedContext.CatalogReleaseEvents.Add(releaseEvent);

            var notification = new UserReleaseNotification
            {
                Id = Guid.NewGuid(),
                UserId = userA.Id,
                TvShowId = tvShow.Id,
                NotificationType = UserReleaseNotificationType.NewEpisodes,
                Status = UserReleaseNotificationStatus.Pending,
                AggregationWindowKey = "2026-09-15",
                CreatedAtUtc = DateTime.UtcNow
            };
            seedContext.UserReleaseNotifications.Add(notification);
            await seedContext.SaveChangesAsync();

            userAId = userA.Id;
            userBId = userB.Id;
            notificationId = notification.Id;
            releaseEventId = releaseEvent.Id;
        }

        await using var mismatchContext = ReleaseNotificationFanoutFixture.CreateContext();
        mismatchContext.UserReleaseNotificationEvents.Add(new UserReleaseNotificationEvent
        {
            UserReleaseNotificationId = notificationId,
            CatalogReleaseEventId = releaseEventId,
            UserId = userBId
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => mismatchContext.SaveChangesAsync());

        await using var validContext = ReleaseNotificationFanoutFixture.CreateContext();
        validContext.UserReleaseNotificationEvents.Add(new UserReleaseNotificationEvent
        {
            UserReleaseNotificationId = notificationId,
            CatalogReleaseEventId = releaseEventId,
            UserId = userAId
        });

        await validContext.SaveChangesAsync();
        Assert.Equal(1, await validContext.UserReleaseNotificationEvents.CountAsync());
    }

    private static async Task<(Guid TvShowId, Guid UserId, Guid ReleaseEventId)> SeedFollowedShowWithEventAsync(
        bool notifyNewEpisodes = true,
        CatalogReleaseEventSource source = CatalogReleaseEventSource.BoundaryDetection,
        DateTime? notifyFromUtc = null)
    {
        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        var user = User.Create(Guid.NewGuid(), "fanout@example.com", "hash", "Fanout User", DateTime.UtcNow);
        var tvShowId = Guid.NewGuid();
        context.Users.Add(user);
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = "Fanout Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var follow = TvShowFollow.Create(user.Id, tvShowId, true, notifyNewEpisodes, DateTime.UtcNow);
        follow.SetNotifyFromUtc(
            notifyFromUtc ?? new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        context.TvShowFollows.Add(follow);

        var releaseEvent = CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShowId,
            1,
            5,
            ReleaseDate,
            source,
            DateTime.UtcNow);
        context.CatalogReleaseEvents.Add(releaseEvent);
        await context.SaveChangesAsync();

        return (tvShowId, user.Id, releaseEvent.Id);
    }

    private static async Task<(Guid TvShowId, Guid UserId, Guid FirstEventId, Guid SecondEventId)>
        SeedFollowedShowWithTwoEpisodeEventsAsync()
    {
        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        var user = User.Create(Guid.NewGuid(), "aggregate@example.com", "hash", "Aggregate User", DateTime.UtcNow);
        var tvShowId = Guid.NewGuid();
        context.Users.Add(user);
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = "Aggregate Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var follow = TvShowFollow.Create(user.Id, tvShowId, true, true, DateTime.UtcNow);
        follow.SetNotifyFromUtc(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        context.TvShowFollows.Add(follow);

        var first = CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShowId, 1, 1, ReleaseDate, CatalogReleaseEventSource.BoundaryDetection, DateTime.UtcNow);
        var second = CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShowId, 1, 2, ReleaseDate, CatalogReleaseEventSource.BoundaryDetection, DateTime.UtcNow);
        context.CatalogReleaseEvents.AddRange(first, second);
        await context.SaveChangesAsync();

        return (tvShowId, user.Id, first.Id, second.Id);
    }

    private static async Task<(Guid TvShowId, Guid UserAId, Guid UserBId, Guid ReleaseEventId)>
        SeedShowWithTwoFollowersAsync()
    {
        await using var context = ReleaseNotificationFanoutFixture.CreateContext();
        var userA = User.Create(Guid.NewGuid(), "follower-a@example.com", "hash", "Follower A", DateTime.UtcNow);
        var userB = User.Create(Guid.NewGuid(), "follower-b@example.com", "hash", "Follower B", DateTime.UtcNow);
        var tvShowId = Guid.NewGuid();
        context.Users.AddRange(userA, userB);
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            Title = "Multi Follower Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        foreach (var user in new[] { userA, userB })
        {
            var follow = TvShowFollow.Create(user.Id, tvShowId, true, true, DateTime.UtcNow);
            follow.SetNotifyFromUtc(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
            follow.EstablishBaseline(DateTime.UtcNow);
            context.TvShowFollows.Add(follow);
        }

        var releaseEvent = CatalogReleaseEventFactory.CreateEpisodeEvent(
            tvShowId,
            1,
            5,
            ReleaseDate,
            CatalogReleaseEventSource.BoundaryDetection,
            DateTime.UtcNow);
        context.CatalogReleaseEvents.Add(releaseEvent);
        await context.SaveChangesAsync();

        return (tvShowId, userA.Id, userB.Id, releaseEvent.Id);
    }
}
