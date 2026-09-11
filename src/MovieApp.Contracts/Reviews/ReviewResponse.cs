namespace MovieApp.Contracts.Reviews;

public sealed record ReviewResponse(
    Guid Id,
    ReviewAuthorResponse User,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);
