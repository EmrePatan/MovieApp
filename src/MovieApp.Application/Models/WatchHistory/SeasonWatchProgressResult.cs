namespace MovieApp.Application.Models.WatchHistory;

public sealed record SeasonWatchProgressResult(
    Guid TvShowId,
    int SeasonNumber,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage,
    SeasonNextEpisodeResult? NextEpisode);
