using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.ReleaseNotifications;

public readonly record struct ReleaseNotificationBucketKey(
    Guid UserId,
    Guid TvShowId,
    UserReleaseNotificationType NotificationType,
    string AggregationWindowKey);
