using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class WatchedMovieRepository(ApplicationDbContext dbContext) : IWatchedMovieRepository
{
    public async Task<WatchedMovie?> GetByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedMovies
            .FirstOrDefaultAsync(
                watchedMovie => watchedMovie.UserId == userId && watchedMovie.MovieId == movieId,
                cancellationToken);
    }

    public async Task<(WatchedMovie Entity, bool Created)> UpsertAsync(
        WatchedMovie watchedMovie,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.WatchedMovies
            .FirstOrDefaultAsync(
                item => item.UserId == watchedMovie.UserId && item.MovieId == watchedMovie.MovieId,
                cancellationToken);

        if (existing is not null)
        {
            existing.UpdateWatchedAt(watchedMovie.WatchedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (existing, false);
        }

        try
        {
            dbContext.WatchedMovies.Add(watchedMovie);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (watchedMovie, true);
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            var raced = await dbContext.WatchedMovies
                .FirstAsync(
                    item => item.UserId == watchedMovie.UserId && item.MovieId == watchedMovie.MovieId,
                    cancellationToken);

            raced.UpdateWatchedAt(watchedMovie.WatchedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (raced, false);
        }
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var watchedMovie = await dbContext.WatchedMovies
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.MovieId == movieId,
                cancellationToken);

        if (watchedMovie is null)
        {
            return false;
        }

        dbContext.WatchedMovies.Remove(watchedMovie);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<WatchedMovie> Items, int TotalCount)> GetUserWatchedMoviesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(watchedMovie => watchedMovie.Movie)
            .OrderByDescending(watchedMovie => watchedMovie.WatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<(Guid MovieId, string Title, DateTime WatchedAt)>> GetRecentForUserAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .OrderByDescending(watchedMovie => watchedMovie.WatchedAt)
            .Take(take)
            .Select(watchedMovie => new ValueTuple<Guid, string, DateTime>(
                watchedMovie.MovieId,
                watchedMovie.Movie.Title,
                watchedMovie.WatchedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .CountAsync(watchedMovie => watchedMovie.UserId == userId, cancellationToken);
    }
}
