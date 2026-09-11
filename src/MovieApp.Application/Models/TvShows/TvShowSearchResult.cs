namespace MovieApp.Application.Models.TvShows;

public sealed record TvShowSearchResult(
    Guid Id,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
    string Title,
    string? OriginalTitle,
    string? Overview,
    DateOnly? FirstAirDate,
    string? PosterPath,
    string? BackdropPath,
    string? OriginalLanguage,
    decimal VoteAverage,
    int VoteCount);
