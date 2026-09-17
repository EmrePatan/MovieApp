namespace MovieApp.Contracts.AiRecommendations;

public sealed record AiRecommendationMovieItemResponse(
    Guid Id,
    string Type,
    string Title,
    string? OriginalTitle,
    string? Overview,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount,
    int? Year,
    int? RuntimeMinutes,
    string Reason);
