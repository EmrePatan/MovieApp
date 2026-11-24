using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Social;

/// <summary>
/// Sends a device push for in-app social notifications (likes, comments, replies, follows).
/// Best effort: a push failure never fails the action that created the notification.
/// </summary>
public interface ISocialPushNotifier
{
    Task NotifyAsync(
        IReadOnlyCollection<SocialNotification> notifications,
        CancellationToken cancellationToken = default);
}
