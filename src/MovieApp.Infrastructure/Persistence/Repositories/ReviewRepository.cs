using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(ApplicationDbContext dbContext) : IReviewRepository
{
    public async Task<Review?> GetByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.MovieId == movieId,
                cancellationToken);
    }

    public async Task<Review?> GetByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<Review?> GetTrackedByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.MovieId == movieId,
                cancellationToken);
    }

    public async Task<Review?> GetTrackedByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .Include(review => review.User)
            .FirstOrDefaultAsync(
                review => review.UserId == userId && review.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<bool> ExistsForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .AnyAsync(
                review => review.UserId == userId && review.MovieId == movieId,
                cancellationToken);
    }

    public async Task<bool> ExistsForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .AnyAsync(
                review => review.UserId == userId && review.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<Review> AddAsync(Review review, CancellationToken cancellationToken = default)
    {
        review.ValidateInvariants();
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        return review;
    }

    public async Task UpdateAsync(Review review, CancellationToken cancellationToken = default)
    {
        review.ValidateInvariants();
        dbContext.Reviews.Update(review);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.MovieId == movieId,
                cancellationToken);

        if (review is null)
        {
            return false;
        }

        dbContext.Reviews.Remove(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.TvShowId == tvShowId,
                cancellationToken);

        if (review is null)
        {
            return false;
        }

        dbContext.Reviews.Remove(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<Review> Reviews, int TotalCount)> GetPublicReviewsForMovieAsync(
        Guid movieId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.MovieId == movieId);

        return await GetPublicReviewsAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<Review> Reviews, int TotalCount)> GetPublicReviewsForTvShowAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.TvShowId == tvShowId);

        return await GetPublicReviewsAsync(query, page, pageSize, cancellationToken);
    }

    private static async Task<(IReadOnlyList<Review> Reviews, int TotalCount)> GetPublicReviewsAsync(
        IQueryable<Review> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var reviews = await query
            .Include(review => review.User)
            .OrderByDescending(review => review.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (reviews, totalCount);
    }
}
