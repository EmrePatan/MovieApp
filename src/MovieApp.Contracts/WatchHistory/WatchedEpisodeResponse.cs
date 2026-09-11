namespace MovieApp.Contracts.WatchHistory;

public sealed record WatchedEpisodeResponse(
    Guid EpisodeId,
    Guid TvShowId,
    Guid SeasonId,
    string TvShowTitle,
    int SeasonNumber,
    int EpisodeNumber,
    string? EpisodeTitle,
    DateTime WatchedAt);
