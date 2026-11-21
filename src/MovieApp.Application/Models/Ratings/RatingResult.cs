namespace MovieApp.Application.Models.Ratings;

public sealed record RatingResult(
    Guid Id,
    Guid? MovieId,
    Guid? TvShowId,
    Guid? EpisodeId,
    int Score,
    DateTime CreatedAt,
    DateTime UpdatedAt);
