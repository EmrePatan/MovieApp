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

    public static UserReviewListItemResult ToUserReviewListItemResult(UserReviewCatalogListItem item)
    {
        var review = item.Review;

        if (review.MovieId is Guid movieId && review.Movie is not null)
        {
            return new UserReviewListItemResult(
                review.Id,
                "movie",
                movieId,
                review.Movie.Title,
                review.Movie.PosterPath,
                review.Movie.ReleaseDate,
                review.Content,
                review.CreatedAt,
                review.UpdatedAt,
                item.UserRating);
        }

        if (review.TvShowId is Guid tvShowId && review.TvShow is not null)
        {
            return new UserReviewListItemResult(
                review.Id,
                "tv",
                tvShowId,
                review.TvShow.Title,
                review.TvShow.PosterPath,
                review.TvShow.FirstAirDate,
                review.Content,
                review.CreatedAt,
                review.UpdatedAt,
                item.UserRating);
        }

        throw new InvalidOperationException("Review must reference a movie or TV show with catalog data loaded.");
    }
}
