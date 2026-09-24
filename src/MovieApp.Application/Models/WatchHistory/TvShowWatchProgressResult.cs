namespace MovieApp.Application.Models.WatchHistory;

public sealed record TvShowWatchProgressResult(
    Guid TvShowId,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage,
    int RegularTotalEpisodes,
    int RegularWatchedEpisodes,
    bool IsFullyWatched,
    NextEpisodeResult? NextEpisode,
    IReadOnlyList<SeasonProgressSummaryResult> Seasons,
    bool IsCompleted);
