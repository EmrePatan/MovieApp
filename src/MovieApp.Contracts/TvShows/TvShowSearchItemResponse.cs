using MovieApp.Contracts.Movies;

namespace MovieApp.Contracts.TvShows;

public sealed record TvShowSearchItemResponse(
    Guid Id,
    ExternalIdsResponse ExternalIds,
    string Title,
    string? OriginalTitle,
    string? Overview,
    DateOnly? FirstAirDate,
    string? PosterPath,
    string? BackdropPath,
    string? OriginalLanguage,
    decimal VoteAverage,
    int VoteCount);
