using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class RecommendationDismissalRepository(ApplicationDbContext dbContext) : IRecommendationDismissalRepository
{
    public async Task<bool> ExistsForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.RecommendationDismissals
            .AsNoTracking()
            .AnyAsync(
                dismissal => dismissal.UserId == userId && dismissal.MovieId == movieId,
                cancellationToken);
    }

    public async Task<bool> ExistsForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.RecommendationDismissals
            .AsNoTracking()
            .AnyAsync(
                dismissal => dismissal.UserId == userId && dismissal.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<bool> TryAddAsync(RecommendationDismissal dismissal, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.RecommendationDismissals.Add(dismissal);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            return false;
        }
    }
}
