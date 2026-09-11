namespace MovieApp.Application.Models.Recommendations;

public sealed record RecommendationItem(
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
