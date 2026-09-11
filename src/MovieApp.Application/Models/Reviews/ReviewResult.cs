namespace MovieApp.Application.Models.Reviews;

public sealed record ReviewResult(
    Guid Id,
    Guid? MovieId,
    Guid? TvShowId,
    ReviewAuthorResult Author,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);
