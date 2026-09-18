namespace MovieApp.Contracts.Reviews;

public sealed record ReviewRatingDistributionResponse(
    decimal AverageScore,
    int RatedReviewCount,
    IReadOnlyDictionary<int, int> ScoreDistribution);
