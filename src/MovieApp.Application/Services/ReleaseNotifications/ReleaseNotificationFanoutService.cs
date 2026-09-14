using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.ReleaseNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseNotifications;

public sealed class ReleaseNotificationFanoutService(
    IReleaseNotificationFanoutRepository repository,
    IMovieReleaseFollowCleanupService movieReleaseFollowCleanupService) : IReleaseNotificationFanoutService
{
    private sealed record EligibleFanoutLink(CatalogReleaseEvent ReleaseEvent, CatalogFollow Follow);

    public async Task<ReleaseNotificationFanoutResult> ProcessAsync(
        IReadOnlyCollection<Guid> catalogReleaseEventIds,
        CancellationToken cancellationToken = default)
    {
        if (catalogReleaseEventIds.Count == 0)
        {
            return ReleaseNotificationFanoutResult.Empty;
        }

        var events = await repository.GetEventsByIdsAsync(catalogReleaseEventIds, cancellationToken);
        var eventsProcessed = events.Count;

        var fanoutableEvents = events
            .Where(releaseEvent => ReleaseNotificationEligibility.CanFanOutSource(releaseEvent.Source))
            .ToList();
        var skippedBySource = events.Count - fanoutableEvents.Count;

        if (fanoutableEvents.Count == 0)
        {
            return new ReleaseNotificationFanoutResult(
                eventsProcessed,
                0,
                0,
                0,
                0,
                0,
                skippedBySource);
        }

        var tvShowIds = fanoutableEvents
            .Where(releaseEvent => releaseEvent.TvShowId.HasValue)
            .Select(releaseEvent => releaseEvent.TvShowId!.Value)
            .Distinct()
            .ToList();

        var movieIds = fanoutableEvents
            .Where(releaseEvent => releaseEvent.MovieId.HasValue)
            .Select(releaseEvent => releaseEvent.MovieId!.Value)
            .Distinct()
            .ToList();

        var tvFollows = await repository.GetEstablishedFollowsByTvShowIdsAsync(tvShowIds, cancellationToken);
        var movieFollows = await repository.GetMovieFollowsByMovieIdsAsync(movieIds, cancellationToken);
        var tvTitles = await repository.GetTvShowTitlesByIdsAsync(tvShowIds, cancellationToken);
        var movieTitles = await repository.GetMovieTitlesByIdsAsync(movieIds, cancellationToken);

        var followsByTvShow = tvFollows
            .GroupBy(follow => follow.ContentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var followsByMovie = movieFollows
            .GroupBy(follow => follow.ContentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var eligibleLinks = new List<EligibleFanoutLink>();
        var skippedByPreference = 0;
        var skippedByBoundary = 0;

        foreach (var releaseEvent in fanoutableEvents)
        {
            var showFollows = releaseEvent.TvShowId.HasValue &&
                              followsByTvShow.TryGetValue(releaseEvent.TvShowId.Value, out var tvShowFollows)
                ? tvShowFollows
                : [];

            var movieShowFollows = releaseEvent.MovieId.HasValue &&
                                   followsByMovie.TryGetValue(releaseEvent.MovieId.Value, out var movieShowFollowsList)
                ? movieShowFollowsList
                : [];

            foreach (var follow in showFollows.Concat(movieShowFollows))
            {
                if (!ReleaseNotificationEligibility.MatchesPreference(releaseEvent, follow))
                {
                    skippedByPreference++;
                    continue;
                }

                if (!ReleaseNotificationEligibility.IsWithinNotificationBoundary(releaseEvent, follow))
                {
                    skippedByBoundary++;
                    continue;
                }

                eligibleLinks.Add(new EligibleFanoutLink(releaseEvent, follow));
            }
        }

        if (eligibleLinks.Count == 0)
        {
            await CleanupMovieReleaseFollowsAsync(fanoutableEvents, cancellationToken);

            return new ReleaseNotificationFanoutResult(
                eventsProcessed,
                0,
                0,
                0,
                skippedByPreference,
                skippedByBoundary,
                skippedBySource);
        }

        var eligibleFollowers = eligibleLinks
            .Select(link => link.Follow.UserId)
            .Distinct()
            .Count();

        var existingLinks = await repository.GetExistingEventLinksAsync(
            fanoutableEvents.Select(releaseEvent => releaseEvent.Id).ToList(),
            cancellationToken);

        var bucketGroups = eligibleLinks
            .GroupBy(link => CreateBucketKey(link.ReleaseEvent, link.Follow))
            .ToList();

        var bucketKeys = bucketGroups.Select(group => group.Key).ToList();
        var existingNotifications = await repository.GetNotificationsByBucketsAsync(bucketKeys, cancellationToken);
        var existingNotificationMap = existingNotifications.ToDictionary(CreateBucketKeyFromNotification);

        var newNotifications = new List<UserReleaseNotification>();
        var notificationsToUpdate = new List<UserReleaseNotification>();
        var newEventLinks = new List<UserReleaseNotificationEvent>();
        var pendingNotifications = new Dictionary<ReleaseNotificationBucketKey, UserReleaseNotification>();

        foreach (var bucketGroup in bucketGroups)
        {
            if (!existingNotificationMap.TryGetValue(bucketGroup.Key, out var notification) &&
                !pendingNotifications.TryGetValue(bucketGroup.Key, out notification))
            {
                notification = new UserReleaseNotification
                {
                    Id = Guid.NewGuid(),
                    UserId = bucketGroup.Key.UserId,
                    TvShowId = bucketGroup.Key.TvShowId,
                    MovieId = bucketGroup.Key.MovieId,
                    NotificationType = bucketGroup.Key.NotificationType,
                    Status = UserReleaseNotificationStatus.Pending,
                    AggregationWindowKey = bucketGroup.Key.AggregationWindowKey,
                    CreatedAtUtc = DateTime.UtcNow
                };
                newNotifications.Add(notification);
                pendingNotifications[bucketGroup.Key] = notification;
            }

            var addedLinks = 0;
            foreach (var link in bucketGroup)
            {
                if (existingLinks.Contains((link.Follow.UserId, link.ReleaseEvent.Id)))
                {
                    continue;
                }

                newEventLinks.Add(new UserReleaseNotificationEvent
                {
                    UserReleaseNotificationId = notification!.Id,
                    CatalogReleaseEventId = link.ReleaseEvent.Id,
                    UserId = link.Follow.UserId
                });
                addedLinks++;
            }

            if (addedLinks == 0)
            {
                continue;
            }

            var existingCount = existingNotificationMap.TryGetValue(bucketGroup.Key, out var persistedNotification)
                ? persistedNotification.NotificationEvents.Count
                : 0;
            var totalEventCount = existingCount + addedLinks;
            var contentTitle = ResolveContentTitle(bucketGroup.Key, tvTitles, movieTitles);
            var (title, body) = ReleaseNotificationContentBuilder.Build(
                contentTitle,
                bucketGroup.Key.NotificationType,
                totalEventCount);

            notification!.Title = title;
            notification.Body = body;

            if (existingNotificationMap.ContainsKey(bucketGroup.Key))
            {
                notificationsToUpdate.Add(notification);
            }
        }

        var persistResult = await repository.UpsertNotificationsAndLinksAsync(
            newNotifications,
            notificationsToUpdate,
            newEventLinks,
            cancellationToken);

        await CleanupMovieReleaseFollowsAsync(fanoutableEvents, cancellationToken);

        return new ReleaseNotificationFanoutResult(
            eventsProcessed,
            eligibleFollowers,
            persistResult.NotificationsCreated,
            persistResult.EventLinksCreated,
            skippedByPreference,
            skippedByBoundary,
            skippedBySource);
    }

    private async Task CleanupMovieReleaseFollowsAsync(
        IReadOnlyList<CatalogReleaseEvent> fanoutableEvents,
        CancellationToken cancellationToken)
    {
        var movieReleaseMovieIds = fanoutableEvents
            .Where(releaseEvent => releaseEvent.EventType == CatalogReleaseEventType.MovieReleased)
            .Where(releaseEvent => releaseEvent.MovieId.HasValue)
            .Select(releaseEvent => releaseEvent.MovieId!.Value)
            .Distinct()
            .ToList();

        if (movieReleaseMovieIds.Count == 0)
        {
            return;
        }

        await movieReleaseFollowCleanupService.CleanupAsync(movieReleaseMovieIds, cancellationToken);
    }

    private static ReleaseNotificationBucketKey CreateBucketKey(
        CatalogReleaseEvent releaseEvent,
        CatalogFollow follow) =>
        new(
            follow.UserId,
            releaseEvent.TvShowId,
            releaseEvent.MovieId,
            ReleaseNotificationEligibility.MapNotificationType(releaseEvent.EventType),
            ReleaseNotificationAggregation.BuildWindowKey(releaseEvent.ReleaseAtUtc));

    private static ReleaseNotificationBucketKey CreateBucketKeyFromNotification(
        UserReleaseNotification notification) =>
        new(
            notification.UserId,
            notification.TvShowId,
            notification.MovieId,
            notification.NotificationType,
            notification.AggregationWindowKey);

    private static string ResolveContentTitle(
        ReleaseNotificationBucketKey bucketKey,
        IReadOnlyDictionary<Guid, string> tvTitles,
        IReadOnlyDictionary<Guid, string> movieTitles)
    {
        if (bucketKey.TvShowId.HasValue)
        {
            return tvTitles.GetValueOrDefault(bucketKey.TvShowId.Value, "TV Show");
        }

        if (bucketKey.MovieId.HasValue)
        {
            return movieTitles.GetValueOrDefault(bucketKey.MovieId.Value, "Movie");
        }

        return "Release";
    }
}
