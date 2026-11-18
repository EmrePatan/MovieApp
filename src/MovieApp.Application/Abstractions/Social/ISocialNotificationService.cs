namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialNotificationItem(
    Guid Id, string NotificationType, Guid ActorId, string UserName, string DisplayName, string? AvatarUrl,
    Guid? ActivityId, Guid? CommentId, Guid? FollowId, DateTime CreatedAt, bool IsRead);

public sealed record SocialNotificationPage(IReadOnlyList<SocialNotificationItem> Items, int Page, int PageSize, int UnreadCount);

public interface ISocialNotificationService
{
    Task<SocialNotificationPage> GetAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
}