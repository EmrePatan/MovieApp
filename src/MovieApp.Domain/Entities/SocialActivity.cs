using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class SocialActivity
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public SocialActivityType ActivityType { get; set; }
    public Guid SourceEntityId { get; set; }
    public Guid TargetContentId { get; set; }
    public string TargetContentType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public static SocialActivity Create(
        Guid actorId,
        SocialActivityType activityType,
        Guid sourceEntityId,
        Guid targetContentId,
        string targetContentType,
        DateTime utcNow)
    {
        if (actorId == Guid.Empty || sourceEntityId == Guid.Empty || targetContentId == Guid.Empty)
            throw new ArgumentException("Social activity identifiers are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(targetContentType);
        if (targetContentType.Length > 16)
            throw new ArgumentOutOfRangeException(nameof(targetContentType));

        return new SocialActivity
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            ActivityType = activityType,
            SourceEntityId = sourceEntityId,
            TargetContentId = targetContentId,
            TargetContentType = targetContentType,
            CreatedAt = utcNow,
            IsDeleted = false
        };
    }

    public void Retract() => IsDeleted = true;
}
