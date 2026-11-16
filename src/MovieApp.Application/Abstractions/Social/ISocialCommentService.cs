namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialCommentResult(Guid Id, Guid ActivityId, Guid AuthorId, string UserName, string DisplayName,
    Guid? ParentCommentId, string Content, DateTime CreatedAt, DateTime? UpdatedAt, bool IsDeleted);

public interface ISocialCommentService
{
    Task<IReadOnlyList<SocialCommentResult>> GetAsync(Guid activityId, Guid? parentCommentId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SocialCommentResult> CreateAsync(Guid activityId, Guid? parentCommentId, string content, CancellationToken cancellationToken = default);
    Task<SocialCommentResult> EditAsync(Guid commentId, string content, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid commentId, CancellationToken cancellationToken = default);
}