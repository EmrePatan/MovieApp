namespace MovieApp.Contracts.Ratings;

public sealed record RatingResponse(
    Guid Id,
    Guid? MovieId,
    Guid? TvShowId,
    int Score,
    DateTime CreatedAt,
    DateTime UpdatedAt);
