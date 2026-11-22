namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialFeedItemResult(
    Guid Id, Guid ActorId, string UserName, string DisplayName, string? AvatarUrl,
    string ActivityType, DateTime CreatedAt,
    Guid TargetContentId, string TargetContentType, string TargetTitle,
    string? TargetOriginalTitle, string? TargetSubtitle, string? PosterPath, int? RatingScore, string? ReviewExcerpt,
    int LikesCount, int CommentsCount, bool IsLikedByMe,
    Guid? CatalogNavigationId, string? CatalogNavigationType,
    int? CatalogSeasonNumber = null,
    int? CatalogEpisodeNumber = null,
    Guid? ReviewId = null, string? ReviewAuthoringLocale = null, DateTime? ReviewUpdatedAt = null,
    SocialCommentResult? HighlightComment = null,
    string? HighlightCommentParentContent = null,
    string? HighlightCommentParentDisplayName = null,
    int? HighlightCommentAuthorRatingScore = null);

public sealed record SocialFeedPageResult(IReadOnlyList<SocialFeedItemResult> Items, string? NextCursor);

public interface ISocialFeedService
{
    Task<SocialFeedPageResult> GetFeedAsync(
        string? cursor,
        int pageSize,
        SocialFeedMode mode = SocialFeedMode.Mixed,
        string? contentLocale = null,
        CancellationToken cancellationToken = default);
    Task<SocialFeedPageResult> GetUserActivitiesAsync(
        Guid userId,
        string? cursor,
        int pageSize,
        string? contentLocale = null,
        CancellationToken cancellationToken = default);
    Task<SocialFeedItemResult> GetActivityAsync(
        Guid activityId,
        string? contentLocale = null,
        CancellationToken cancellationToken = default);
}
