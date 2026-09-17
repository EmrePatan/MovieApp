namespace MovieApp.Application.Models.Insights;

public sealed record InsightsSummaryResult(
    DateTime MemberSince,
    IReadOnlyList<InsightsMovieDnaLabelResult> MovieDna,
    InsightsSummaryStatsResult Summary,
    InsightsWatchingMixResult WatchingMix,
    DateTime GeneratedAtUtc);
