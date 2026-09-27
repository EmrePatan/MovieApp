namespace MovieApp.Application.Models.Reviews;

public sealed record UserReviewListItemResult(
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
