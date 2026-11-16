namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialLikeResult(bool IsLiked, int LikeCount);

public interface ISocialLikeService
{
    Task<SocialLikeResult> LikeActivityAsync(Guid activityId, CancellationToken cancellationToken = default);
    Task<SocialLikeResult> UnlikeActivityAsync(Guid activityId, CancellationToken cancellationToken = default);
    Task<SocialLikeResult> LikeCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
    Task<SocialLikeResult> UnlikeCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
}
