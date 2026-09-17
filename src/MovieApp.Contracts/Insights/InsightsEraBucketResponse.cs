namespace MovieApp.Contracts.Insights;

public sealed record InsightsEraBucketResponse(
    string Bucket,
    int Count,
    decimal? Percent);
