namespace MovieApp.Application.Models.Providers;

public sealed record MovieProviderSummary(
    string ExternalId,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
    string Title,
    string? Overview,
    DateOnly? ReleaseDate,
    string? PosterPath,
    decimal VoteAverage,
    int VoteCount,
    string? OriginalTitle = null,
    decimal Popularity = 0,
    string? OriginalLanguage = null,
    string? PrimaryOriginCountryCode = null,
    IReadOnlyList<int>? GenreTmdbIds = null);
