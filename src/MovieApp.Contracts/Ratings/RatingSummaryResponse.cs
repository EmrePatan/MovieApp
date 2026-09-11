namespace MovieApp.Contracts.Ratings;

public sealed record RatingSummaryResponse(
    decimal AverageScore,
    int RatingCount,
    IReadOnlyDictionary<int, int> ScoreDistribution);
