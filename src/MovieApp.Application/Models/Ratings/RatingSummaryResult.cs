namespace MovieApp.Application.Models.Ratings;

public sealed record RatingSummaryResult(
    decimal AverageScore,
    int RatingCount,
    IReadOnlyDictionary<int, int> ScoreDistribution);
