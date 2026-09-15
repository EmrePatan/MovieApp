using MovieApp.Contracts.Movies;

namespace MovieApp.Contracts.TvShows;

public sealed record TvShowDetailsResponse(
    Guid Id,
    ExternalIdsResponse ExternalIds,
    string Title,
    string? OriginalTitle,
    string? Overview,
    DateOnly? FirstAirDate,
    DateOnly? LastAirDate,
    string? PosterPath,
    string? BackdropPath,
    string? OriginalLanguage,
    decimal VoteAverage,
    int VoteCount,
    string Status,
    IReadOnlyList<string> Genres,
    IReadOnlyList<SeasonSummaryResponse> Seasons,
    bool CanFollow);
