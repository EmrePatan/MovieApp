namespace MovieApp.Contracts.Search;

public sealed record SearchItemResponse(
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
    int? TmdbId = null,
    string? KnownForDepartment = null);
