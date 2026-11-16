using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Social;

public interface ISocialActivityRecorder
{
    Task RecordAsync(
        Guid actorId,
        SocialActivityType activityType,
        Guid sourceEntityId,
        Guid targetContentId,
        string targetContentType,
        DateTime createdAt,
        CancellationToken cancellationToken = default);

    Task RetractAsync(Guid actorId, SocialActivityType activityType, Guid targetContentId, CancellationToken cancellationToken = default);
}
