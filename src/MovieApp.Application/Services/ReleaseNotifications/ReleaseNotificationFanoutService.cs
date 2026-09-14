using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.ReleaseNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseNotifications;

public sealed class ReleaseNotificationFanoutService(
    IReleaseNotificationFanoutRepository repository) : IReleaseNotificationFanoutService
{
    private sealed record EligibleFanoutLink(CatalogReleaseEvent ReleaseEvent, TvShowFollow Follow);

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

        var tvShowIds = fanoutableEvents.Select(releaseEvent => releaseEvent.TvShowId).Distinct().ToList();
        var follows = await repository.GetEstablishedFollowsByTvShowIdsAsync(tvShowIds, cancellationToken);
        var titles = await repository.GetTvShowTitlesByIdsAsync(tvShowIds, cancellationToken);
        var followsByTvShow = follows
            .GroupBy(follow => follow.TvShowId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var eligibleLinks = new List<EligibleFanoutLink>();
        var skippedByPreference = 0;
        var skippedByBoundary = 0;

        foreach (var releaseEvent in fanoutableEvents)
        {
            if (!followsByTvShow.TryGetValue(releaseEvent.TvShowId, out var showFollows))
            {
                continue;
            }

            foreach (var follow in showFollows)
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
            .GroupBy(link => new ReleaseNotificationBucketKey(
                link.Follow.UserId,
                link.ReleaseEvent.TvShowId,
                ReleaseNotificationEligibility.MapNotificationType(link.ReleaseEvent.EventType),
                ReleaseNotificationAggregation.BuildWindowKey(link.ReleaseEvent.ReleaseAtUtc)))
            .ToList();

        var bucketKeys = bucketGroups.Select(group => group.Key).ToList();
        var existingNotifications = await repository.GetNotificationsByBucketsAsync(bucketKeys, cancellationToken);
        var existingNotificationMap = existingNotifications.ToDictionary(
            notification => new ReleaseNotificationBucketKey(
                notification.UserId,
                notification.TvShowId,
                notification.NotificationType,
                notification.AggregationWindowKey));

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
            var tvShowTitle = titles.GetValueOrDefault(bucketGroup.Key.TvShowId, "TV Show");
            var (title, body) = ReleaseNotificationContentBuilder.Build(
                tvShowTitle,
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

        return new ReleaseNotificationFanoutResult(
            eventsProcessed,
            eligibleFollowers,
            persistResult.NotificationsCreated,
            persistResult.EventLinksCreated,
            skippedByPreference,
            skippedByBoundary,
            skippedBySource);
    }
}
