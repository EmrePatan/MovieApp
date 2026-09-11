namespace MovieApp.Application.Models.Providers;

public sealed record SeasonProviderDetails(
    string ExternalTvShowId,
    int? TmdbId,
    int? TvdbId,
    int SeasonNumber,
    string? Name,
    string? Overview,
    DateOnly? AirDate,
    int? EpisodeCount,
    string? PosterPath,
    IReadOnlyList<EpisodeProviderDetails> Episodes);
