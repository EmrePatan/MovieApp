namespace MovieApp.Application.Models.TvShows;

public sealed record TvShowDetailsResult(
    Guid Id,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
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
    IReadOnlyList<SeasonSummaryResult> Seasons);
