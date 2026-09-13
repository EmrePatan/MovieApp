namespace MovieApp.Contracts.WatchHistory;

public sealed record SeasonWatchedEpisodesResponse(
    Guid TvShowId,
    int SeasonNumber,
    IReadOnlyList<Guid> WatchedEpisodeIds);
