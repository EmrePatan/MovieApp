namespace MovieApp.Contracts.Collections;

public sealed record CollectionResponse(
    int TmdbId,
    string Name,
    string? Overview,
    string? PosterPath,
    string? BackdropPath,
    IReadOnlyList<CollectionPartResponse> Parts);

public sealed record CollectionPartResponse(
    Guid Id,
    int TmdbId,
    string Title,
    string? OriginalTitle,
    string? Overview,
    string? PosterPath,
    string? BackdropPath,
    string? ReleaseDate,
    decimal VoteAverage,
    int VoteCount);
