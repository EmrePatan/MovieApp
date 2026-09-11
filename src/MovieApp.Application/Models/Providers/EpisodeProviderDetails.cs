namespace MovieApp.Application.Models.Providers;

public sealed record EpisodeProviderDetails(
    string ExternalTvShowId,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Name,
    string? Overview,
    DateOnly? AirDate,
    int? RuntimeMinutes,
    string? StillPath,
    decimal VoteAverage,
    int VoteCount);
