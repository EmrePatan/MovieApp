namespace MovieApp.Application.Models.Reviews;

public sealed record ReviewResult(
    Guid Id,
    Guid? MovieId,
    Guid? TvShowId,
    ReviewAuthorResult Author,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? UserRating = null,
    string? AuthoringLocale = null,
    Guid? SocialActivityId = null,
    int LikesCount = 0,
    int CommentsCount = 0,
    bool IsLikedByMe = false);
