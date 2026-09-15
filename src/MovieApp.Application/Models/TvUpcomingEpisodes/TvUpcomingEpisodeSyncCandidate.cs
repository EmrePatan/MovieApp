namespace MovieApp.Application.Models.TvUpcomingEpisodes;

public sealed record TvUpcomingEpisodeSyncCandidate(
    Guid TvShowId,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId);
