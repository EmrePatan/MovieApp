using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialFeedItemResult(
    Guid Id, Guid ActorId, string UserName, string DisplayName, string? AvatarUrl,
    SocialActivityType ActivityType, DateTime CreatedAt,
    Guid TargetContentId, string TargetContentType, string TargetTitle,
    string? TargetSubtitle, string? PosterPath, int? RatingScore, string? ReviewExcerpt,
    int LikesCount, int CommentsCount, bool IsLikedByMe);

public sealed record SocialFeedPageResult(IReadOnlyList<SocialFeedItemResult> Items, string? NextCursor);

public interface ISocialFeedService
{
    Task<SocialFeedPageResult> GetFeedAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default);
}
