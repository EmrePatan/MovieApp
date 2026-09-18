using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Reviews;
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

    public async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForMovieAsync(
        Guid movieId,
        int page,
        int pageSize,
        ReviewListSort sort,
        CancellationToken cancellationToken = default)
    {
        var joinedQuery =
            from review in dbContext.Reviews
                .AsNoTracking()
                .Include(item => item.User)
                .Where(item => item.MovieId == movieId)
            join rating in dbContext.Ratings
                .AsNoTracking()
                .Where(item => item.MovieId == movieId)
                on review.UserId equals rating.UserId into ratings
            from rating in ratings.DefaultIfEmpty()
            select new PublicReviewListItem(
                review,
                rating == null ? null : rating.Score);

        return await GetPublicReviewsAsync(joinedQuery, page, pageSize, sort, cancellationToken);
    }

    public async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForTvShowAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        ReviewListSort sort,
        CancellationToken cancellationToken = default)
    {
        var joinedQuery =
            from review in dbContext.Reviews
                .AsNoTracking()
                .Include(item => item.User)
                .Where(item => item.TvShowId == tvShowId)
            join rating in dbContext.Ratings
                .AsNoTracking()
                .Where(item => item.TvShowId == tvShowId)
                on review.UserId equals rating.UserId into ratings
            from rating in ratings.DefaultIfEmpty()
            select new PublicReviewListItem(
                review,
                rating == null ? null : rating.Score);

        return await GetPublicReviewsAsync(joinedQuery, page, pageSize, sort, cancellationToken);
    }

    private static async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsAsync(
        IQueryable<PublicReviewListItem> query,
        int page,
        int pageSize,
        ReviewListSort sort,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var orderedQuery = ApplySort(query, sort);

        var reviews = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (reviews, totalCount);
    }

    private static IQueryable<PublicReviewListItem> ApplySort(
        IQueryable<PublicReviewListItem> query,
        ReviewListSort sort)
    {
        return sort switch
        {
            ReviewListSort.Oldest => query
                .OrderBy(item => item.Review.CreatedAt),
            ReviewListSort.RatingDesc => query
                .OrderByDescending(item => item.UserRating ?? -1)
                .ThenByDescending(item => item.Review.CreatedAt),
            ReviewListSort.RatingAsc => query
                .OrderBy(item => item.UserRating ?? int.MaxValue)
                .ThenByDescending(item => item.Review.CreatedAt),
            _ => query
                .OrderByDescending(item => item.Review.CreatedAt),
        };
    }
}
