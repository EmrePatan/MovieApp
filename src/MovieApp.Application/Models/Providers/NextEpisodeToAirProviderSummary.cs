namespace MovieApp.Application.Models.Providers;

public sealed record NextEpisodeToAirProviderSummary(
    int? TmdbId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Name,
    DateOnly? AirDate);
