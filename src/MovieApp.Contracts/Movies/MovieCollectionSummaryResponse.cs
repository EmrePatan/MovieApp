namespace MovieApp.Contracts.Movies;

public sealed record MovieCollectionSummaryResponse(
    int TmdbId,
    string Name,
    string? PosterPath,
    string? BackdropPath);
