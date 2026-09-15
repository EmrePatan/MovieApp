using MovieApp.Application.Models.Collections;

namespace MovieApp.Application.Models.Movies;

public sealed record MovieDetailsResult(
    Guid Id,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
    string Title,
    string? OriginalTitle,
    string? Overview,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? PosterPath,
    string? BackdropPath,
    string? OriginalLanguage,
    decimal VoteAverage,
    int VoteCount,
    IReadOnlyList<string> Genres,
    MovieCollectionSummaryResult? Collection);
