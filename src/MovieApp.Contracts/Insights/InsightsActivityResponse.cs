namespace MovieApp.Contracts.Insights;

public sealed record InsightsActivityResponse(
    IReadOnlyList<InsightsActivityDayResponse> Days,
    InsightsActivitySummaryResponse Summary);
