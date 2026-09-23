using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Mapping;
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
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        var reviewsQuery = dbContext.Reviews
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.MovieId == movieId);

        var ratingsQuery = dbContext.Ratings
            .AsNoTracking()
            .Where(item => item.MovieId == movieId);

        return await GetPublicReviewsAsync(
            reviewsQuery,
            ratingsQuery,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsForTvShowAsync(
        Guid tvShowId,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars = null,
        CancellationToken cancellationToken = default)
    {
        var reviewsQuery = dbContext.Reviews
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.TvShowId == tvShowId);

        var ratingsQuery = dbContext.Ratings
            .AsNoTracking()
            .Where(item => item.TvShowId == tvShowId);

        return await GetPublicReviewsAsync(
            reviewsQuery,
            ratingsQuery,
            page,
            pageSize,
            sort,
            ratingStars,
            cancellationToken);
    }

    private static async Task<(IReadOnlyList<PublicReviewListItem> Reviews, int TotalCount)> GetPublicReviewsAsync(
        IQueryable<Review> reviewsQuery,
        IQueryable<Rating> ratingsQuery,
        int page,
        int pageSize,
        ReviewListSort sort,
        int? ratingStars,
        CancellationToken cancellationToken)
    {
        var query =
            from review in reviewsQuery
            join rating in ratingsQuery on review.UserId equals rating.UserId into ratings
            from rating in ratings.DefaultIfEmpty()
            select new { review, rating };

        if (ratingStars.HasValue)
        {
            var stars = ratingStars.Value;
            query = query.Where(item =>
                item.rating != null &&
                (item.rating.Score + 1) / 2 == stars);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orderedQuery = sort switch
        {
            ReviewListSort.Oldest => query.OrderBy(item => item.review.CreatedAt),
            ReviewListSort.RatingDesc => query
                .OrderByDescending(item => item.rating == null ? -1 : item.rating.Score)
                .ThenByDescending(item => item.review.CreatedAt),
            ReviewListSort.RatingAsc => query
                .OrderBy(item => item.rating == null ? int.MaxValue : item.rating.Score)
                .ThenByDescending(item => item.review.CreatedAt),
            _ => query.OrderByDescending(item => item.review.CreatedAt),
        };

        var rows = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyList<PublicReviewListItem> reviews = rows
            .Select(item => new PublicReviewListItem(item.review, item.rating?.Score))
            .ToList();

        return (reviews, totalCount);
    }

    public async Task<ReviewTranslationSource?> GetTranslationSourceByIdAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.Id == reviewId)
            .Select(review => new ReviewTranslationSource(
                review.Id,
                review.Content,
                review.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
