namespace MovieApp.Application.Models.Reviews;

public sealed record ReviewRatingDistributionResult(
    decimal AverageScore,
    int RatedReviewCount,
    IReadOnlyDictionary<int, int> ScoreDistribution);
