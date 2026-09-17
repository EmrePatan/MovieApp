namespace MovieApp.Contracts.Insights;

public sealed record InsightsEstimatedTimeWatchedResponse(
    int TotalEstimatedMinutes,
    int MovieEstimatedMinutes,
    int EpisodeEstimatedMinutes,
    int KnownRuntimeItemCount,
    int TotalWatchedItemCount,
    decimal CoveragePercent,
    int? CurrentYearEstimatedMinutes);
