namespace MovieApp.Application.Models.Providers;

public sealed record MovieProviderDetails(
    string ExternalId,
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
    int? TmdbCollectionId = null,
    string? CollectionName = null,
    string? CollectionPosterPath = null,
    string? CollectionBackdropPath = null,
    IReadOnlyList<ProviderKeywordSummary>? Keywords = null);
