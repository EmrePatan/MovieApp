namespace MovieApp.Application.Models.WatchHistory;

public sealed record SeasonWatchedEpisodesResult(
    Guid TvShowId,
    int SeasonNumber,
    IReadOnlyList<Guid> WatchedEpisodeIds);
