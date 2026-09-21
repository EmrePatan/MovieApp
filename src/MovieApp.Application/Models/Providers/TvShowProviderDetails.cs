namespace MovieApp.Application.Models.Providers;

public sealed record TvShowProviderDetails(
    string ExternalId,
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
    IReadOnlyList<SeasonProviderSummary> Seasons,
    NextEpisodeToAirProviderSummary? NextEpisodeToAir = null,
    IReadOnlyList<ProviderKeywordSummary>? Keywords = null);
