namespace MovieApp.Contracts.Reviews;

public sealed record ReviewResponse(
    Guid Id,
    ReviewAuthorResponse User,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? UserRating = null,
    string? AuthoringLocale = null,
    Guid? SocialActivityId = null,
    int LikesCount = 0,
    int CommentsCount = 0,
    bool IsLikedByMe = false);
