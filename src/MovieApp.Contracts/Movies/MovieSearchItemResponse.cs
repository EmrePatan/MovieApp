namespace MovieApp.Contracts.Movies;

public sealed record MovieSearchItemResponse(
    Guid Id,
    ExternalIdsResponse ExternalIds,
    string Title,
    string? Overview,
    DateOnly? ReleaseDate,
    string? PosterPath,
    decimal VoteAverage,
    int VoteCount);
