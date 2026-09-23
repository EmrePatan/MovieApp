namespace MovieApp.Application.Models.Providers;

public sealed record TvShowProviderSummary(
    string ExternalId,
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
    int VoteCount,
    decimal Popularity = 0);
