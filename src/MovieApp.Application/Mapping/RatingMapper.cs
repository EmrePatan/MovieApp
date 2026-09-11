using MovieApp.Application.Models.Ratings;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Ratings;

namespace MovieApp.Application.Mapping;

public static class RatingMapper
{
    public static RatingResult ToResult(Rating rating) =>
        new(
            rating.Id,
            rating.MovieId,
            rating.TvShowId,
            rating.Score,
            rating.CreatedAt,
            rating.UpdatedAt);

    public static RatingSummaryResult ToEmptySummary() =>
        new(
            0m,
            0,
            CreateEmptyDistribution());

    public static RatingSummaryResult ToSummary(
        int ratingCount,
        decimal averageScore,
        IReadOnlyDictionary<int, int> distribution) =>
        new(averageScore, ratingCount, distribution);

    public static IReadOnlyDictionary<int, int> CreateEmptyDistribution()
    {
        var distribution = new Dictionary<int, int>();
        for (var score = RatingScoreRules.MinScore; score <= RatingScoreRules.MaxScore; score++)
        {
            distribution[score] = 0;
        }

        return distribution;
    }
}
