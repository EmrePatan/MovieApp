namespace MovieApp.Domain.Entities;

/// <summary>
/// Per-user push opt-outs. A missing row means every category is enabled.
/// </summary>
public sealed class UserNotificationPreference
{
    public Guid UserId { get; set; }

    public bool MessagePushEnabled { get; set; } = true;

    public bool ReleasePushEnabled { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
