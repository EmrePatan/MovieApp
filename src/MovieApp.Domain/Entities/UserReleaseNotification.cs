using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class UserReleaseNotification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? TvShowId { get; set; }

    public Guid? MovieId { get; set; }

    public UserReleaseNotificationType NotificationType { get; set; }

    public UserReleaseNotificationStatus Status { get; set; }

    public string AggregationWindowKey { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Body { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public User User { get; set; } = null!;

    public TvShow? TvShow { get; set; }

    public Movie? Movie { get; set; }

    public ICollection<UserReleaseNotificationEvent> NotificationEvents { get; set; } = [];
}
