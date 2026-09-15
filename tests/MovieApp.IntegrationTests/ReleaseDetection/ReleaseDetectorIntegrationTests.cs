using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.ReleaseDetection;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.ReleaseDetection;

[CollectionDefinition("ReleaseDetector")]
public sealed class ReleaseDetectorTestsDefinition : ICollectionFixture<ReleaseDetectorFixture>;

[Collection("ReleaseDetector")]
public sealed class ReleaseDetectorIntegrationTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    [Fact]
    public async Task BaselineAbsorbCreatesHistoricalEventsWithoutNotifications()
    {
        await ReleaseDetectorFixture.ResetAsync();

        var tvShowId = await SeedShowAsync(
            CreateSeason(1, episodes: CreateEpisode(1, new DateOnly(2026, 8, 1))),
            CreateSeason(2, episodes: CreateEpisode(1, new DateOnly(2026, 9, 1))));

        var result = await ScanAsync(tvShowId, ReleaseDetectionMode.BaselineAbsorb, Today);

        Assert.True(result.EventsCreated >= 3);

        await using var context = ReleaseDetectorFixture.CreateContext();
        Assert.True(await context.CatalogReleaseEvents.AnyAsync());
        Assert.False(await context.UserReleaseNotifications.AnyAsync());
        Assert.False(await context.UserReleaseNotificationEvents.AnyAsync());
        Assert.All(
            await context.CatalogReleaseEvents.ToListAsync(),
            releaseEvent => Assert.Equal(CatalogReleaseEventSource.BaselineAbsorb, releaseEvent.Source));
    }

    [Fact]
    public async Task FutureKnownEpisodeCreatesEventWhenBoundaryCrosses()
    {
        await ReleaseDetectorFixture.ResetAsync();

        var tvShowId = await SeedShowAsync(
            CreateSeason(1, episodes: CreateEpisode(5, Today)));

        var beforeBoundary = await ScanAsync(tvShowId, ReleaseDetectionMode.BoundaryCheck, Today.AddDays(-1));
        Assert.Equal(0, beforeBoundary.EventsCreated);

        var onBoundary = await ScanAsync(tvShowId, ReleaseDetectionMode.BoundaryCheck, Today);
        Assert.Equal(2, onBoundary.EventsCreated);
        Assert.Equal(1, onBoundary.EpisodeEventsCreated);
        Assert.Equal(1, onBoundary.SeasonPremiereEventsCreated);
    }

    [Fact]
    public async Task RepeatedScanIsIdempotent()
    {
        await ReleaseDetectorFixture.ResetAsync();

        var tvShowId = await SeedShowAsync(
            CreateSeason(1, episodes: CreateEpisode(5, Today)));

        var first = await ScanAsync(tvShowId, ReleaseDetectionMode.BoundaryCheck, Today);
        var second = await ScanAsync(tvShowId, ReleaseDetectionMode.BoundaryCheck, Today);

        Assert.Equal(2, first.EventsCreated);
        Assert.Equal(0, second.EventsCreated);
        Assert.Equal(2, second.EventsAlreadyExisted);

        await using var context = ReleaseDetectorFixture.CreateContext();
        Assert.Equal(2, await context.CatalogReleaseEvents.CountAsync());
    }

    [Fact]
    public async Task SeasonZeroNeverCreatesReleaseEvents()
    {
        await ReleaseDetectorFixture.ResetAsync();

        var tvShowId = await SeedShowAsync(
            CreateSeason(0, airDate: Today, episodeCount: 3, episodes: CreateEpisode(1, Today)));

        var result = await ScanAsync(tvShowId, ReleaseDetectionMode.BoundaryCheck, Today);

        Assert.Equal(0, result.EventsCreated);

        await using var context = ReleaseDetectorFixture.CreateContext();
        Assert.Equal(0, await context.CatalogReleaseEvents.CountAsync());
    }

    [Fact]
    public async Task ConcurrentScansKeepSingleReleaseEvent()
    {
        await ReleaseDetectorFixture.ResetAsync();

        var tvShowId = await SeedShowAsync(
            CreateSeason(1, episodes: CreateEpisode(1, Today)));

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => ScanAsync(tvShowId, ReleaseDetectionMode.BoundaryCheck, Today))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Contains(results, result => result.EventsCreated > 0);
        Assert.All(results, result => Assert.True(result.EventsCreated + result.EventsAlreadyExisted > 0));

        await using var context = ReleaseDetectorFixture.CreateContext();
        Assert.Equal(2, await context.CatalogReleaseEvents.CountAsync());
    }

    [Fact]
    public async Task JunctionRejectsNotificationUserMismatch()
    {
        await ReleaseDetectorFixture.ResetAsync();

        Guid userAId;
        Guid userBId;
        Guid notificationId;
        Guid releaseEventId;

        await using (var seedContext = ReleaseDetectorFixture.CreateContext())
        {
            var userA = User.Create(Guid.NewGuid(), "user-a@example.com", "hash", "User A", DateTime.UtcNow);
            var userB = User.Create(Guid.NewGuid(), "user-b@example.com", "hash", "User B", DateTime.UtcNow);
            var tvShow = new TvShow
            {
                Id = Guid.NewGuid(),
                Title = "Mismatch Show",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            seedContext.Users.AddRange(userA, userB);
            seedContext.TvShows.Add(tvShow);

            var releaseEvent = new CatalogReleaseEvent
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShow.Id,
                EventType = CatalogReleaseEventType.NewEpisode,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                ReleaseAtUtc = DateTime.UtcNow,
                DetectedAtUtc = DateTime.UtcNow,
                Source = CatalogReleaseEventSource.BoundaryDetection,
                DedupeKey = CatalogReleaseEventDedupeKey.ForEpisode(tvShow.Id, 1, 1)
            };

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

            seedContext.CatalogReleaseEvents.Add(releaseEvent);
            seedContext.UserReleaseNotifications.Add(notification);
            await seedContext.SaveChangesAsync();

            userAId = userA.Id;
            userBId = userB.Id;
            notificationId = notification.Id;
            releaseEventId = releaseEvent.Id;
        }

        await using var mismatchContext = ReleaseDetectorFixture.CreateContext();
        mismatchContext.UserReleaseNotificationEvents.Add(new UserReleaseNotificationEvent
        {
            UserReleaseNotificationId = notificationId,
            CatalogReleaseEventId = releaseEventId,
            UserId = userBId
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => mismatchContext.SaveChangesAsync());

        await using var validContext = ReleaseDetectorFixture.CreateContext();
        validContext.UserReleaseNotificationEvents.Add(new UserReleaseNotificationEvent
        {
            UserReleaseNotificationId = notificationId,
            CatalogReleaseEventId = releaseEventId,
            UserId = userAId
        });
        await validContext.SaveChangesAsync();
    }

    private static async Task<ReleaseDetectionResult> ScanAsync(
        Guid tvShowId,
        ReleaseDetectionMode mode,
        DateOnly boundary)
    {
        await using var context = ReleaseDetectorFixture.CreateContext();
        var detector = new ReleaseDetector(
            new ReleaseDetectionCatalogRepository(context),
            new CatalogReleaseEventRepository(context));

        return await detector.ScanTvShowAsync(tvShowId, mode, boundary);
    }

    private static async Task<Guid> SeedShowAsync(params Season[] seasons)
    {
        await using var context = ReleaseDetectorFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var tvShow = new TvShow
        {
            Id = tvShowId,
            Title = "Detector Show",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);

        foreach (var season in seasons)
        {
            season.Id = Guid.NewGuid();
            season.TvShowId = tvShowId;
            season.CreatedAt = DateTime.UtcNow;
            season.UpdatedAt = DateTime.UtcNow;

            foreach (var episode in season.Episodes)
            {
                episode.Id = Guid.NewGuid();
                episode.SeasonId = season.Id;
                episode.CreatedAt = DateTime.UtcNow;
                episode.UpdatedAt = DateTime.UtcNow;
            }

            context.Seasons.Add(season);
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static Season CreateSeason(
        int seasonNumber,
        DateOnly? airDate = null,
        int? episodeCount = null,
        params Episode[] episodes) =>
        new()
        {
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            Episodes = episodes.ToList()
        };

    private static Episode[] CreateEpisode(int episodeNumber, DateOnly airDate) =>
    [
        new()
        {
            EpisodeNumber = episodeNumber,
            AirDate = airDate
        }
    ];
}
