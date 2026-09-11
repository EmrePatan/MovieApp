using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Ratings;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Ratings;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class RatingRepository(ApplicationDbContext dbContext) : IRatingRepository
{
    public async Task<Rating?> GetByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Ratings
            .FirstOrDefaultAsync(
                rating => rating.UserId == userId && rating.MovieId == movieId,
                cancellationToken);
    }

    public async Task<Rating?> GetByUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Ratings
            .FirstOrDefaultAsync(
                rating => rating.UserId == userId && rating.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<Rating> AddAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        rating.ValidateInvariants();
        dbContext.Ratings.Add(rating);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rating;
    }

    public async Task UpdateAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        rating.ValidateInvariants();
        dbContext.Ratings.Update(rating);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var rating = await GetByUserAndMovieAsync(userId, movieId, cancellationToken);
        if (rating is null)
        {
            return false;
        }

        dbContext.Ratings.Remove(rating);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var rating = await GetByUserAndTvShowAsync(userId, tvShowId, cancellationToken);
        if (rating is null)
        {
            return false;
        }

        dbContext.Ratings.Remove(rating);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<RatingSummaryResult> GetSummaryForMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken = default) =>
        GetSummaryAsync(dbContext.Ratings.AsNoTracking().Where(rating => rating.MovieId == movieId), cancellationToken);

    public Task<RatingSummaryResult> GetSummaryForTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        GetSummaryAsync(dbContext.Ratings.AsNoTracking().Where(rating => rating.TvShowId == tvShowId), cancellationToken);

    private static async Task<RatingSummaryResult> GetSummaryAsync(
        IQueryable<Rating> query,
        CancellationToken cancellationToken)
    {
        var ratingCount = await query.CountAsync(cancellationToken);
        if (ratingCount == 0)
        {
            return RatingMapper.ToEmptySummary();
        }

        var averageScore = await query.AverageAsync(rating => (decimal)rating.Score, cancellationToken);
        var roundedAverage = decimal.Round(averageScore, 2, MidpointRounding.AwayFromZero);

        var distributionRows = await query
            .GroupBy(rating => rating.Score)
            .Select(group => new { Score = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var distribution = RatingMapper.CreateEmptyDistribution().ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var row in distributionRows)
        {
            distribution[row.Score] = row.Count;
        }

        return RatingMapper.ToSummary(ratingCount, roundedAverage, distribution);
    }
}
