using MovieApp.Application.Models.Reviews;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class ReviewMapper
{
    public static ReviewResult ToResult(Review review, int? userRating = null) =>
        new(
            review.Id,
            review.MovieId,
            review.TvShowId,
            new ReviewAuthorResult(review.User.Id, review.User.DisplayName),
            review.Content,
            review.CreatedAt,
            review.UpdatedAt,
            userRating,
            review.AuthoringLocale);
}
