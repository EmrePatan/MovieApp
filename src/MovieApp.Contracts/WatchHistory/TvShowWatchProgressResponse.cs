namespace MovieApp.Contracts.WatchHistory;

public sealed record TvShowWatchProgressResponse(
    Guid TvShowId,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage,
    NextEpisodeResponse? NextEpisode,
    IReadOnlyList<TvShowSeasonProgressResponse> Seasons);
