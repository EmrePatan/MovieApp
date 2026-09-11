using MovieApp.Application.Models.Ratings;

namespace MovieApp.Application.Services.Ratings;

public interface IRatingService
{
    Task<RatingUpsertResult> UpsertMovieRatingAsync(
        Guid movieId,
        int score,
        CancellationToken cancellationToken = default);

    Task<RatingUpsertResult> UpsertTvShowRatingAsync(
        Guid tvShowId,
        int score,
        CancellationToken cancellationToken = default);

    Task DeleteMovieRatingAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task DeleteTvShowRatingAsync(Guid tvShowId, CancellationToken cancellationToken = default);

    Task<RatingResult> GetCurrentUserMovieRatingAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<RatingResult> GetCurrentUserTvShowRatingAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<RatingSummaryResult> GetMovieRatingSummaryAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<RatingSummaryResult> GetTvShowRatingSummaryAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
