namespace MovieApp.Contracts.WatchHistory;

public sealed record TvShowWatchProgressResponse(
    Guid TvShowId,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage,
    int RegularTotalEpisodes,
    int RegularWatchedEpisodes,
    bool IsFullyWatched,
    NextEpisodeResponse? NextEpisode,
    IReadOnlyList<TvShowSeasonProgressResponse> Seasons);
