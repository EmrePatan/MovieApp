namespace MovieApp.Application.Models.Recommendations;

public sealed record PersonalizedCandidateProfile(
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
    IReadOnlyList<Guid> GenreIds,
    IReadOnlyDictionary<Guid, string> GenreNames,
    IReadOnlyList<Guid> PersonIds);
