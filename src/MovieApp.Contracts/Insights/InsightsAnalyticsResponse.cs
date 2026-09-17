namespace MovieApp.Contracts.Insights;

public sealed record InsightsAnalyticsResponse(
    InsightsActivityResponse Activity,
    InsightsTasteResponse Taste,
    InsightsErasResponse Eras,
    InsightsEstimatedTimeWatchedResponse EstimatedTimeWatched,
    InsightsRatingsAnalyticsResponse Ratings,
    IReadOnlyList<InsightsMilestoneResponse> Milestones,
    DateTime GeneratedAtUtc);
