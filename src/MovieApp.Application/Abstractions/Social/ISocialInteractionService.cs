using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialCommentResult(
    Guid Id, Guid ActivityId, Guid UserId, string UserName, string DisplayName, string? AvatarUrl,
    Guid? ParentCommentId, string Content, DateTime CreatedAt, DateTime? UpdatedAt,
    int LikesCount, int RepliesCount, bool IsLikedByMe, string? AuthoringLocale);

public sealed record SocialCommentPageResult(IReadOnlyList<SocialCommentResult> Items, int Page, int PageSize, bool HasMore);

public interface ISocialInteractionService
{
    Task SetActivityLikeAsync(Guid activityId, bool isLiked, CancellationToken cancellationToken = default);
    Task<SocialCommentPageResult> GetCommentsAsync(Guid activityId, Guid? parentCommentId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SocialCommentResult> CreateCommentAsync(
        Guid activityId,
        Guid? parentCommentId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
    Task SetCommentLikeAsync(Guid commentId, bool isLiked, CancellationToken cancellationToken = default);
    Task ReportAsync(string targetType, Guid targetId, string reason, string? details, CancellationToken cancellationToken = default);
}
