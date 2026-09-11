namespace MovieApp.Application.Models.Movies;

public sealed record MovieSearchResult(
    Guid Id,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
    string Title,
    string? Overview,
    DateOnly? ReleaseDate,
    string? PosterPath,
    decimal VoteAverage,
    int VoteCount);
