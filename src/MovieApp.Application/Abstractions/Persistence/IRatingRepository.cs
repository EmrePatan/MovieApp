using MovieApp.Application.Models.Ratings;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IRatingRepository
{
    Task<Rating?> GetByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<Rating?> GetByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<Rating> AddAsync(Rating rating, CancellationToken cancellationToken = default);

    Task UpdateAsync(Rating rating, CancellationToken cancellationToken = default);

    Task<bool> DeleteForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<RatingSummaryResult> GetSummaryForMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<RatingSummaryResult> GetSummaryForTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
