namespace MovieApp.Application.Models.Providers;

public sealed record CollectionProviderDetails(
    int TmdbId,
    string Name,
    string? Overview,
    string? PosterPath,
    string? BackdropPath,
    IReadOnlyList<CollectionProviderPart> Parts);

public sealed record CollectionProviderPart(
    int TmdbId,
    string Title,
    string? OriginalTitle,
    string? Overview,
    string? PosterPath,
    string? BackdropPath,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount,
    bool Adult);
