using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class PushNotificationDelivery
{
    public Guid Id { get; set; }

    public Guid UserReleaseNotificationId { get; set; }

    public Guid PushDeviceId { get; set; }

    public PushNotificationDeliveryStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public string? ExpoTicketId { get; set; }

    public string? LastErrorCode { get; set; }

    public string? LastErrorMessage { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? DeliveredAtUtc { get; set; }

    public DateTime? ClaimedUntilUtc { get; set; }

    public Guid? ClaimToken { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public UserReleaseNotification UserReleaseNotification { get; set; } = null!;

    public PushDevice PushDevice { get; set; } = null!;
}
