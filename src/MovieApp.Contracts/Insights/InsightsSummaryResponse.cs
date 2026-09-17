namespace MovieApp.Contracts.Insights;

public sealed record InsightsSummaryResponse(
    DateTime MemberSince,
    IReadOnlyList<InsightsMovieDnaLabelResponse> MovieDna,
    InsightsSummaryStatsResponse Summary,
    InsightsWatchingMixResponse WatchingMix,
    DateTime GeneratedAtUtc);
