namespace MovieApp.Contracts.Reviews;

public sealed record UserReviewListItemResponse(
    Guid Id,
    string ContentType,
    Guid ContentId,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? UserRating);
