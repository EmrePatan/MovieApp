namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialReviewEngagement(
    Guid ReviewId,
    Guid ActivityId,
    int LikesCount,
    int CommentsCount,
    bool IsLikedByMe);

public interface ISocialReviewEngagementLookup
{
    /// <summary>
    /// Resolves social activity engagement for review source entities (ReviewedMovie / ReviewedTvShow).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SocialReviewEngagement>> GetForReviewIdsAsync(
        IReadOnlyList<Guid> reviewIds,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default);
}
