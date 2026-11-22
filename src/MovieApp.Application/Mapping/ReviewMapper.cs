using MovieApp.Application.Models.Reviews;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class ReviewMapper
{
    public static ReviewResult ToResult(
        Review review,
        int? userRating = null,
        string? effectiveAvatarUrl = null,
        Guid? socialActivityId = null,
        int likesCount = 0,
        int commentsCount = 0,
        bool isLikedByMe = false) =>
        new(
            review.Id,
            review.MovieId,
            review.TvShowId,
            new ReviewAuthorResult(review.User.Id, review.User.DisplayName, effectiveAvatarUrl),
            review.Content,
            review.CreatedAt,
            review.UpdatedAt,
            userRating,
            review.AuthoringLocale,
            socialActivityId,
            likesCount,
            commentsCount,
            isLikedByMe);

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

        if (review.EpisodeId is Guid episodeId && review.Episode is not null)
        {
            var season = review.Episode.Season;
            var show = season?.TvShow;
            var title = show is null
                ? (review.Episode.Name ?? $"E{review.Episode.EpisodeNumber}")
                : $"{show.Title} · S{season!.SeasonNumber} E{review.Episode.EpisodeNumber}";
            return new UserReviewListItemResult(
                review.Id,
                "episode",
                episodeId,
                title,
                show?.PosterPath ?? review.Episode.StillPath,
                review.Episode.AirDate,
                review.Content,
                review.CreatedAt,
                review.UpdatedAt,
                item.UserRating,
                show?.Id ?? season?.TvShowId,
                season?.SeasonNumber,
                review.Episode.EpisodeNumber);
        }

        throw new InvalidOperationException("Review must reference a movie, TV show, or episode with catalog data loaded.");
    }
}
