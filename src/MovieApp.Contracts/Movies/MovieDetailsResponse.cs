using MovieApp.Contracts.Catalog;

namespace MovieApp.Contracts.Movies;

public sealed record MovieDetailsResponse(
    Guid Id,
    ExternalIdsResponse ExternalIds,
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
    IReadOnlyList<CatalogKeywordSummaryResponse> Keywords,
    MovieCollectionSummaryResponse? Collection,
    bool IsReleased,
    bool CanFollowForRelease,
    bool CanSetReleaseAlert);
