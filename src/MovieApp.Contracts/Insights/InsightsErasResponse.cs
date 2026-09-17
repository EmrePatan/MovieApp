namespace MovieApp.Contracts.Insights;

public sealed record InsightsErasResponse(
    IReadOnlyList<InsightsEraBucketResponse> Buckets,
    int UnknownCount);
