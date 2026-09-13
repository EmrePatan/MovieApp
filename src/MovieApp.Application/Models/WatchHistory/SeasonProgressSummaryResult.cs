namespace MovieApp.Application.Models.WatchHistory;

public sealed record SeasonProgressSummaryResult(
    int SeasonNumber,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage);
