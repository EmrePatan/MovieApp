namespace MovieApp.Application.Models.Recommendations;

public sealed record SimilaritySourceProfile(
    Guid Id,
    string Type,
    string Title,
    IReadOnlyList<Guid> GenreIds,
    IReadOnlyDictionary<Guid, string> GenreNames,
    IReadOnlyList<Guid> PersonIds,
    decimal VoteAverage,
    int? Year);
