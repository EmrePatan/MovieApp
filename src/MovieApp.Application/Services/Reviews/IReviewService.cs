using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Reviews;

namespace MovieApp.Application.Services.Reviews;

public interface IReviewService
{
    Task<ReviewResult> CreateMovieReviewAsync(
        Guid movieId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default);

    Task<ReviewResult> CreateTvShowReviewAsync(
        Guid tvShowId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default);

    Task<ReviewResult> UpdateMovieReviewAsync(
        Guid movieId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default);

    Task<ReviewResult> UpdateTvShowReviewAsync(
        Guid tvShowId,
        string content,
        string authoringLocale,
        CancellationToken cancellationToken = default);

    Task DeleteMovieReviewAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task DeleteTvShowReviewAsync(Guid tvShowId, CancellationToken cancellationToken = default);

    Task<ReviewResult> GetCurrentUserMovieReviewAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<ReviewResult> GetCurrentUserTvShowReviewAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<ReviewListPageResult> GetMovieReviewsAsync(
        Guid movieId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default);

    Task<ReviewListPageResult> GetTvShowReviewsAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default);
}
