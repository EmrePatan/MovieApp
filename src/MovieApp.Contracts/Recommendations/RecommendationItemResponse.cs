namespace MovieApp.Contracts.Recommendations;

public sealed record RecommendationItemResponse(
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
    decimal Score,
    string? Reason);
