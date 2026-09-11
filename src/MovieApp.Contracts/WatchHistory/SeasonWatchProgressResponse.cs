namespace MovieApp.Contracts.WatchHistory;

public sealed record SeasonWatchProgressResponse(
    Guid TvShowId,
    int SeasonNumber,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage,
    SeasonNextEpisodeResponse? NextEpisode);
