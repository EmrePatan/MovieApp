namespace MovieApp.Domain.Entities;

public sealed class SocialNotification
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public Guid ActorId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public Guid? ActivityId { get; set; }
    public Guid? CommentId { get; set; }
    public Guid? FollowId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public static SocialNotification Create(
        Guid recipientId, Guid actorId, string notificationType,
        Guid? activityId, Guid? commentId, Guid? followId, DateTime utcNow)
    {
        if (recipientId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Notification user identifiers are required.");
        if (recipientId == actorId) throw new InvalidOperationException("Users cannot notify themselves.");
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationType);
        if (notificationType.Length > 32) throw new ArgumentOutOfRangeException(nameof(notificationType));
        return new SocialNotification
        {
            Id = Guid.NewGuid(), RecipientId = recipientId, ActorId = actorId,
            NotificationType = notificationType, ActivityId = activityId, CommentId = commentId,
            FollowId = followId, CreatedAt = utcNow
        };
    }

    public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;
}
