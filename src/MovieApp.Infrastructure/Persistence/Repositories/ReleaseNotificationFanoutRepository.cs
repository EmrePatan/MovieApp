using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.ReleaseNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ReleaseNotificationFanoutRepository(
    ApplicationDbContext dbContext,
    ICatalogFollowRepository catalogFollowRepository) : IReleaseNotificationFanoutRepository
{
    public async Task<IReadOnlyList<CatalogReleaseEvent>> GetEventsByIdsAsync(
        IReadOnlyCollection<Guid> catalogReleaseEventIds,
        CancellationToken cancellationToken = default)
    {
        if (catalogReleaseEventIds.Count == 0)
        {
            return [];
        }

        return await dbContext.CatalogReleaseEvents
            .AsNoTracking()
            .Where(releaseEvent => catalogReleaseEventIds.Contains(releaseEvent.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<CatalogFollow>> GetEstablishedFollowsByTvShowIdsAsync(
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default) =>
        catalogFollowRepository.GetEstablishedTvFollowsByTvShowIdsAsync(tvShowIds, cancellationToken);

    public async Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsByMovieIdsAsync(
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0)
        {
            return [];
        }

        return await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow =>
                follow.ContentType == CatalogContentType.Movie &&
                movieIds.Contains(follow.ContentId) &&
                follow.NotifyMovieRelease)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetTvShowTitlesByIdsAsync(
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .ToDictionaryAsync(tvShow => tvShow.Id, tvShow => tvShow.Title, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetMovieTitlesByIdsAsync(
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .ToDictionaryAsync(movie => movie.Id, movie => movie.Title, cancellationToken);
    }

    public async Task<IReadOnlyList<UserReleaseNotification>> GetNotificationsByBucketsAsync(
        IReadOnlyCollection<ReleaseNotificationBucketKey> bucketKeys,
        CancellationToken cancellationToken = default)
    {
        if (bucketKeys.Count == 0)
        {
            return [];
        }

        var bucketKeySet = bucketKeys.ToHashSet();
        var tvShowIds = bucketKeys
            .Where(key => key.TvShowId.HasValue)
            .Select(key => key.TvShowId!.Value)
            .Distinct()
            .ToList();

        var movieIds = bucketKeys
            .Where(key => key.MovieId.HasValue)
            .Select(key => key.MovieId!.Value)
            .Distinct()
            .ToList();

        var notifications = await dbContext.UserReleaseNotifications
            .AsNoTracking()
            .Include(notification => notification.NotificationEvents)
            .Where(notification =>
                (notification.TvShowId.HasValue && tvShowIds.Contains(notification.TvShowId.Value)) ||
                (notification.MovieId.HasValue && movieIds.Contains(notification.MovieId.Value)))
            .ToListAsync(cancellationToken);

        return notifications
            .Where(notification => bucketKeySet.Contains(new ReleaseNotificationBucketKey(
                notification.UserId,
                notification.TvShowId,
                notification.MovieId,
                notification.NotificationType,
                notification.AggregationWindowKey)))
            .ToList();
    }

    public async Task<HashSet<(Guid UserId, Guid CatalogReleaseEventId)>> GetExistingEventLinksAsync(
        IReadOnlyCollection<Guid> catalogReleaseEventIds,
        CancellationToken cancellationToken = default)
    {
        if (catalogReleaseEventIds.Count == 0)
        {
            return [];
        }

        var links = await dbContext.UserReleaseNotificationEvents
            .AsNoTracking()
            .Where(notificationEvent => catalogReleaseEventIds.Contains(notificationEvent.CatalogReleaseEventId))
            .Select(notificationEvent => new
            {
                notificationEvent.UserId,
                notificationEvent.CatalogReleaseEventId
            })
            .ToListAsync(cancellationToken);

        return links
            .Select(link => (link.UserId, link.CatalogReleaseEventId))
            .ToHashSet();
    }

    public async Task<(int NotificationsCreated, int EventLinksCreated)> UpsertNotificationsAndLinksAsync(
        IReadOnlyList<UserReleaseNotification> newNotifications,
        IReadOnlyList<UserReleaseNotification> notificationsToUpdate,
        IReadOnlyList<UserReleaseNotificationEvent> newEventLinks,
        CancellationToken cancellationToken = default)
    {
        var notificationsCreated = 0;
        var eventLinksCreated = 0;
        var notificationIdMap = new Dictionary<Guid, Guid>();

        foreach (var notification in newNotifications)
        {
            if (await TryAddNotificationAsync(notification, cancellationToken))
            {
                notificationsCreated++;
                continue;
            }

            var existingNotification = await dbContext.UserReleaseNotifications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.UserId == notification.UserId &&
                        candidate.TvShowId == notification.TvShowId &&
                        candidate.MovieId == notification.MovieId &&
                        candidate.NotificationType == notification.NotificationType &&
                        candidate.AggregationWindowKey == notification.AggregationWindowKey,
                    cancellationToken);

            if (existingNotification is not null)
            {
                notificationIdMap[notification.Id] = existingNotification.Id;
            }
        }

        foreach (var notification in notificationsToUpdate)
        {
            var tracked = await dbContext.UserReleaseNotifications
                .FirstOrDefaultAsync(
                    existing => existing.Id == notification.Id,
                    cancellationToken);

            if (tracked is null)
            {
                continue;
            }

            tracked.Title = notification.Title;
            tracked.Body = notification.Body;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var eventLink in newEventLinks)
        {
            if (notificationIdMap.TryGetValue(eventLink.UserReleaseNotificationId, out var resolvedNotificationId))
            {
                eventLink.UserReleaseNotificationId = resolvedNotificationId;
            }

            if (await TryAddEventLinkAsync(eventLink, cancellationToken))
            {
                eventLinksCreated++;
            }
        }

        return (notificationsCreated, eventLinksCreated);
    }

    private async Task<bool> TryAddNotificationAsync(
        UserReleaseNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.UserReleaseNotifications.Add(notification);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            DetachIfTracked(notification);
            return false;
        }
    }

    private async Task<bool> TryAddEventLinkAsync(
        UserReleaseNotificationEvent eventLink,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.UserReleaseNotificationEvents.Add(eventLink);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            DetachIfTracked(eventLink);
            return false;
        }
    }

    public async Task<IReadOnlyList<Guid>> GetPendingFanoutEventIdsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return [];
        }

        var baselineSource = CatalogReleaseEventSource.BaselineAbsorb.ToString();
        var episodeType = CatalogReleaseEventType.NewEpisode.ToString();
        var seasonType = CatalogReleaseEventType.NewSeasonPremiere.ToString();
        var movieReleasedType = CatalogReleaseEventType.MovieReleased.ToString();
        var tvContentType = CatalogContentType.Tv.ToString();
        var movieContentType = CatalogContentType.Movie.ToString();

        return await dbContext.Database
            .SqlQuery<Guid>($"""
                SELECT pending."Id" AS "Value"
                FROM (
                    SELECT e."Id", MIN(e."DetectedAtUtc") AS detected_at
                    FROM catalog_release_events AS e
                    INNER JOIN catalog_follows AS f ON (
                        (e."TvShowId" IS NOT NULL AND f."ContentType" = {tvContentType} AND f."ContentId" = e."TvShowId")
                        OR (e."MovieId" IS NOT NULL AND f."ContentType" = {movieContentType} AND f."ContentId" = e."MovieId")
                    )
                    WHERE e."Source" <> {baselineSource}
                      AND (
                        (e."EventType" = {episodeType} AND f."ContentType" = {tvContentType} AND f."BaselineEstablishedAtUtc" IS NOT NULL AND f."NotifyFromUtc" IS NOT NULL AND f."NotifyNewEpisodes" = TRUE)
                        OR (e."EventType" = {seasonType} AND f."ContentType" = {tvContentType} AND f."BaselineEstablishedAtUtc" IS NOT NULL AND f."NotifyFromUtc" IS NOT NULL AND f."NotifyNewSeasons" = TRUE)
                        OR (e."EventType" = {movieReleasedType} AND f."ContentType" = {movieContentType} AND f."NotifyMovieRelease" = TRUE)
                      )
                      AND (
                        e."EventType" = {movieReleasedType}
                        OR e."ReleaseAtUtc"::date >= f."NotifyFromUtc"::date
                      )
                      AND NOT EXISTS (
                        SELECT 1
                        FROM user_release_notification_events AS ne
                        WHERE ne."CatalogReleaseEventId" = e."Id"
                          AND ne."UserId" = f."UserId"
                      )
                    GROUP BY e."Id"
                ) AS pending
                ORDER BY pending.detected_at
                LIMIT {batchSize}
                """)
            .ToListAsync(cancellationToken);
    }

    private void DetachIfTracked<TEntity>(TEntity entity) where TEntity : class
    {
        var entry = dbContext.Entry(entity);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }
}
