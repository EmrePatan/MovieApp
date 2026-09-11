using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IReviewRepository
{
    Task<Review?> GetByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<Review?> GetByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<Review?> GetTrackedByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<Review?> GetTrackedByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<Review> AddAsync(Review review, CancellationToken cancellationToken = default);

    Task UpdateAsync(Review review, CancellationToken cancellationToken = default);

    Task<bool> DeleteForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Review> Reviews, int TotalCount)> GetPublicReviewsForMovieAsync(
        Guid movieId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Review> Reviews, int TotalCount)> GetPublicReviewsForTvShowAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
