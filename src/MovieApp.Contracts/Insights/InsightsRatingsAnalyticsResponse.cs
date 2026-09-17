namespace MovieApp.Contracts.Insights;

public sealed record InsightsRatingsAnalyticsResponse(
    int RatingCount,
    decimal? AverageStarRating,
    IReadOnlyList<InsightsRatingsDistributionItemResponse> Distribution,
    int? MostUsedStars);
