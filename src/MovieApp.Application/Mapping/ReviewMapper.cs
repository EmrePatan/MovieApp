using MovieApp.Application.Models.Reviews;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class ReviewMapper
{
    public static ReviewRatingDistributionResult ToEmptyReviewRatingDistribution() =>
        new(
            0m,
            0,
            RatingMapper.CreateEmptyDistribution());

    public static ReviewRatingDistributionResult ToReviewRatingDistribution(
        int ratedReviewCount,
        decimal averageScore,
        IReadOnlyDictionary<int, int> distribution) =>
        new(averageScore, ratedReviewCount, distribution);

    public static ReviewResult ToResult(Review review, int? userRating = null) =>
        new(
            review.Id,
            review.MovieId,
            review.TvShowId,
            new ReviewAuthorResult(review.User.Id, review.User.DisplayName),
            review.Content,
            review.CreatedAt,
            review.UpdatedAt,
            userRating);
}
