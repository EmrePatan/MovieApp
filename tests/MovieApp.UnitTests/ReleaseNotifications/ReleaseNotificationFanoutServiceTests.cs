using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.ReleaseNotifications;
using MovieApp.Application.Services.ReleaseNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

using static MovieApp.UnitTests.ReleaseNotifications.ReleaseNotificationFanoutServiceTestHelpers;

namespace MovieApp.UnitTests.ReleaseNotifications;

public sealed class ReleaseNotificationFanoutServiceTests
{
    private static readonly Guid TvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UserB = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateOnly ReleaseDate = new(2026, 9, 15);

    [Fact]
    public async Task ProcessAsync_BaselineAbsorbEvent_DoesNotCreateNotification()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent(CatalogReleaseEventSource.BaselineAbsorb);
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(UserA));

        var service = CreateService(repository);
        var result = await service.ProcessAsync([releaseEvent.Id]);

        Assert.Equal(0, result.NotificationsCreated);
        Assert.Equal(0, result.EventLinksCreated);
        Assert.Equal(1, result.SkippedBySource);
        Assert.Empty(repository.PersistedNotifications);
    }

    [Fact]
    public async Task ProcessAsync_EpisodePreferenceOff_SkipsNotification()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(UserA, notifyNewEpisodes: false));

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(0, result.NotificationsCreated);
        Assert.Equal(1, result.SkippedByPreference);
    }

    [Fact]
    public async Task ProcessAsync_SeasonPreferenceOff_SkipsNotification()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateSeasonEvent();
        repository.Follows.Add(CreateEstablishedFollow(UserA, notifyNewSeasons: false));
        repository.Events.Add(releaseEvent);

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(0, result.NotificationsCreated);
        Assert.Equal(1, result.SkippedByPreference);
    }

    [Fact]
    public async Task ProcessAsync_EligibleEpisode_CreatesNotification()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(UserA));
        repository.Titles[TvShowId] = "Breaking Bad";

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(1, result.EventLinksCreated);
        var notification = Assert.Single(repository.PersistedNotifications);
        Assert.Equal(UserReleaseNotificationType.NewEpisodes, notification.NotificationType);
        Assert.Equal(UserReleaseNotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public async Task ProcessAsync_EligibleSeasonPremiere_CreatesSeasonNotification()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateSeasonEvent();
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(UserA));

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(UserReleaseNotificationType.NewSeason, Assert.Single(repository.PersistedNotifications).NotificationType);
    }

    [Fact]
    public async Task ProcessAsync_TwoEpisodesSameDay_AggregatesIntoOneNotification()
    {
        var repository = new FakeFanoutRepository();
        var first = CreateEpisodeEvent(episodeNumber: 1);
        var second = CreateEpisodeEvent(episodeNumber: 2);
        repository.Events.AddRange([first, second]);
        repository.Follows.Add(CreateEstablishedFollow(UserA));

        var result = await CreateService(repository).ProcessAsync([first.Id, second.Id]);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(2, result.EventLinksCreated);
        Assert.Equal(2, repository.ExistingLinks.Count);
    }

    [Fact]
    public async Task ProcessAsync_SameEpisodesDifferentDay_CreatesSeparateNotifications()
    {
        var repository = new FakeFanoutRepository();
        var dayOne = CreateEpisodeEvent(episodeNumber: 1, airDate: new DateOnly(2026, 9, 15));
        var dayTwo = CreateEpisodeEvent(episodeNumber: 2, airDate: new DateOnly(2026, 9, 16));
        repository.Events.AddRange([dayOne, dayTwo]);
        repository.Follows.Add(CreateEstablishedFollow(UserA));

        var result = await CreateService(repository).ProcessAsync([dayOne.Id, dayTwo.Id]);

        Assert.Equal(2, result.NotificationsCreated);
        Assert.Equal(2, repository.PersistedNotifications.Count);
    }

    [Fact]
    public async Task ProcessAsync_TwoUsersFollowingSameShow_CreatesOneNotificationPerUser()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);
        repository.Follows.AddRange([CreateEstablishedFollow(UserA), CreateEstablishedFollow(UserB)]);

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(2, result.NotificationsCreated);
        Assert.Equal(2, repository.PersistedNotifications.Count);
    }

    [Fact]
    public async Task ProcessAsync_FollowCreatedSameCalendarDayAsRelease_IsEligible()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(
            UserA,
            notifyFromUtc: new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc)));

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(0, result.SkippedByBoundary);
    }

    [Fact]
    public async Task ProcessAsync_EventBeforeFollowDate_IsSkippedByBoundary()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent(airDate: new DateOnly(2026, 9, 14));
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(
            UserA,
            notifyFromUtc: new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)));

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(0, result.NotificationsCreated);
        Assert.Equal(1, result.SkippedByBoundary);
    }

    [Fact]
    public async Task ProcessAsync_RepeatedFanout_DoesNotDuplicate()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(UserA));

        var service = CreateService(repository);
        await service.ProcessAsync([releaseEvent.Id]);
        var second = await service.ProcessAsync([releaseEvent.Id]);

        Assert.Equal(1, repository.PersistedNotifications.Count);
        Assert.Equal(0, second.NotificationsCreated);
        Assert.Equal(0, second.EventLinksCreated);
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentFanout_DoesNotDuplicate()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);
        repository.Follows.Add(CreateEstablishedFollow(UserA));

        var service = CreateService(repository);
        var results = await Task.WhenAll(
            service.ProcessAsync([releaseEvent.Id]),
            service.ProcessAsync([releaseEvent.Id]));

        Assert.Equal(1, repository.PersistedNotifications.Count);
        Assert.Equal(1, repository.PersistedEventLinks.Sum());
        Assert.True(results.Any(result => result.NotificationsCreated > 0));
    }

    [Fact]
    public async Task ProcessAsync_UnfollowedUser_DoesNotCreateNotification()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(0, result.NotificationsCreated);
    }

    [Fact]
    public async Task ProcessAsync_UnestablishedBaseline_DoesNotCreateNotification()
    {
        var repository = new FakeFanoutRepository();
        var releaseEvent = CreateEpisodeEvent();
        repository.Events.Add(releaseEvent);
        var follow = CatalogFollow.CreateTvFollow(UserA, TvShowId, true, true, DateTime.UtcNow);
        follow.SetNotifyFromUtc(DateTime.UtcNow, DateTime.UtcNow);
        repository.Follows.Add(follow);

        var result = await CreateService(repository).ProcessAsync([releaseEvent.Id]);

        Assert.Equal(0, result.NotificationsCreated);
    }

    private static CatalogReleaseEvent CreateEpisodeEvent(
        CatalogReleaseEventSource source = CatalogReleaseEventSource.BoundaryDetection,
        int episodeNumber = 5,
        DateOnly? airDate = null) =>
        CatalogReleaseEventFactory.CreateEpisodeEvent(
            TvShowId,
            1,
            episodeNumber,
            airDate ?? ReleaseDate,
            source,
            DateTime.UtcNow);

    private static CatalogReleaseEvent CreateSeasonEvent() =>
        CatalogReleaseEventFactory.CreateSeasonPremiereEvent(
            TvShowId,
            2,
            ReleaseDate,
            CatalogReleaseEventSource.BoundaryDetection,
            DateTime.UtcNow);

    private static CatalogFollow CreateEstablishedFollow(
        Guid userId,
        DateTime? notifyFromUtc = null,
        bool notifyNewSeasons = true,
        bool notifyNewEpisodes = true)
    {
        var follow = CatalogFollow.CreateTvFollow(userId, TvShowId, notifyNewSeasons, notifyNewEpisodes, DateTime.UtcNow);
        follow.SetNotifyFromUtc(
            notifyFromUtc ?? new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        return follow;
    }

    private sealed class FakeFanoutRepository : IReleaseNotificationFanoutRepository
    {
        public List<CatalogReleaseEvent> Events { get; } = [];

        public List<CatalogFollow> Follows { get; } = [];

        public Dictionary<Guid, string> Titles { get; } = [];

        public List<UserReleaseNotification> PersistedNotifications { get; } = [];

        public List<int> PersistedEventLinks { get; } = [];

        public HashSet<(Guid UserId, Guid CatalogReleaseEventId)> ExistingLinks { get; } = [];

        public Task<IReadOnlyList<CatalogReleaseEvent>> GetEventsByIdsAsync(
            IReadOnlyCollection<Guid> catalogReleaseEventIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogReleaseEvent>>(
                Events.Where(releaseEvent => catalogReleaseEventIds.Contains(releaseEvent.Id)).ToList());

        public Task<IReadOnlyList<CatalogFollow>> GetEstablishedFollowsByTvShowIdsAsync(
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogFollow>>(
                Follows.Where(follow =>
                    tvShowIds.Contains(follow.ContentId) &&
                    follow.BaselineEstablishedAtUtc != null &&
                    follow.NotifyFromUtc != null).ToList());

        public Task<IReadOnlyDictionary<Guid, string>> GetTvShowTitlesByIdsAsync(
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                Titles.Where(pair => tvShowIds.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));

        public Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsByMovieIdsAsync(
            IReadOnlyCollection<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogFollow>>([]);

        public Task<IReadOnlyDictionary<Guid, string>> GetMovieTitlesByIdsAsync(
            IReadOnlyCollection<Guid> movieIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<IReadOnlyList<UserReleaseNotification>> GetNotificationsByBucketsAsync(
            IReadOnlyCollection<ReleaseNotificationBucketKey> bucketKeys,
            CancellationToken cancellationToken = default)
        {
            var keySet = bucketKeys.ToHashSet();
            var notifications = PersistedNotifications
                .Where(notification => keySet.Contains(new ReleaseNotificationBucketKey(
                    notification.UserId,
                    notification.TvShowId,
                    notification.MovieId,
                    notification.NotificationType,
                    notification.AggregationWindowKey)))
                .Select(notification =>
                {
                    notification.NotificationEvents = ExistingLinks
                        .Where(link => link.UserId == notification.UserId)
                        .Select(link => new UserReleaseNotificationEvent
                        {
                            UserReleaseNotificationId = notification.Id,
                            CatalogReleaseEventId = link.CatalogReleaseEventId,
                            UserId = notification.UserId
                        })
                        .ToList();
                    return notification;
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<UserReleaseNotification>>(notifications);
        }

        public Task<HashSet<(Guid UserId, Guid CatalogReleaseEventId)>> GetExistingEventLinksAsync(
            IReadOnlyCollection<Guid> catalogReleaseEventIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingLinks
                .Where(link => catalogReleaseEventIds.Contains(link.CatalogReleaseEventId))
                .ToHashSet());

        public Task<IReadOnlyList<Guid>> GetPendingFanoutEventIdsAsync(
            int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<(int NotificationsCreated, int EventLinksCreated)> UpsertNotificationsAndLinksAsync(
            IReadOnlyList<UserReleaseNotification> newNotifications,
            IReadOnlyList<UserReleaseNotification> notificationsToUpdate,
            IReadOnlyList<UserReleaseNotificationEvent> newEventLinks,
            CancellationToken cancellationToken = default)
        {
            var notificationsCreated = 0;
            foreach (var notification in newNotifications)
            {
                if (PersistedNotifications.Any(existing =>
                        existing.UserId == notification.UserId &&
                        existing.TvShowId == notification.TvShowId &&
                        existing.MovieId == notification.MovieId &&
                        existing.NotificationType == notification.NotificationType &&
                        existing.AggregationWindowKey == notification.AggregationWindowKey))
                {
                    continue;
                }

                PersistedNotifications.Add(notification);
                notificationsCreated++;
            }

            foreach (var notification in notificationsToUpdate)
            {
                var existing = PersistedNotifications.Single(item => item.Id == notification.Id);
                existing.Title = notification.Title;
                existing.Body = notification.Body;
            }

            var eventLinksCreated = 0;
            foreach (var eventLink in newEventLinks)
            {
                if (ExistingLinks.Contains((eventLink.UserId, eventLink.CatalogReleaseEventId)))
                {
                    continue;
                }

                ExistingLinks.Add((eventLink.UserId, eventLink.CatalogReleaseEventId));
                eventLinksCreated++;
            }

            PersistedEventLinks.Add(eventLinksCreated);
            return Task.FromResult((notificationsCreated, eventLinksCreated));
        }
    }
}
