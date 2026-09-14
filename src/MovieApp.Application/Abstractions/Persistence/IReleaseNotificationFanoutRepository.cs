using MovieApp.Application.Models.ReleaseNotifications;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IReleaseNotificationFanoutRepository
{
    Task<IReadOnlyList<CatalogReleaseEvent>> GetEventsByIdsAsync(
        IReadOnlyCollection<Guid> catalogReleaseEventIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TvShowFollow>> GetEstablishedFollowsByTvShowIdsAsync(
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetTvShowTitlesByIdsAsync(
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserReleaseNotification>> GetNotificationsByBucketsAsync(
        IReadOnlyCollection<ReleaseNotificationBucketKey> bucketKeys,
        CancellationToken cancellationToken = default);

    Task<HashSet<(Guid UserId, Guid CatalogReleaseEventId)>> GetExistingEventLinksAsync(
        IReadOnlyCollection<Guid> catalogReleaseEventIds,
        CancellationToken cancellationToken = default);

    Task<(int NotificationsCreated, int EventLinksCreated)> UpsertNotificationsAndLinksAsync(
        IReadOnlyList<UserReleaseNotification> newNotifications,
        IReadOnlyList<UserReleaseNotification> notificationsToUpdate,
        IReadOnlyList<UserReleaseNotificationEvent> newEventLinks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetPendingFanoutEventIdsAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
