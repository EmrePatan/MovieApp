namespace MovieApp.Application.Models.Collections;

public sealed record CollectionDetailResult(
    int TmdbId,
    string Name,
    string? Overview,
    string? PosterPath,
    string? BackdropPath,
    IReadOnlyList<CollectionPartResult> Parts);

public sealed record CollectionPartResult(
    Guid Id,
    int TmdbId,
    string Title,
    string? OriginalTitle,
    string? Overview,
    string? PosterPath,
    string? BackdropPath,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount);

public sealed record MovieCollectionSummaryResult(
    int TmdbId,
    string Name,
    string? PosterPath,
    string? BackdropPath);
