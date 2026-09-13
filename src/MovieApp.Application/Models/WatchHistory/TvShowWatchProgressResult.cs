namespace MovieApp.Application.Models.WatchHistory;

public sealed record TvShowWatchProgressResult(
    Guid TvShowId,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage,
    NextEpisodeResult? NextEpisode,
    IReadOnlyList<SeasonProgressSummaryResult> Seasons);
