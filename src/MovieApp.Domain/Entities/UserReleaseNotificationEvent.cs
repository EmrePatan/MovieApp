namespace MovieApp.Domain.Entities;

public sealed class UserReleaseNotificationEvent
{
    public Guid UserReleaseNotificationId { get; set; }

    public Guid CatalogReleaseEventId { get; set; }

    public Guid UserId { get; set; }

    public UserReleaseNotification Notification { get; set; } = null!;

    public CatalogReleaseEvent ReleaseEvent { get; set; } = null!;

    public User User { get; set; } = null!;
}
