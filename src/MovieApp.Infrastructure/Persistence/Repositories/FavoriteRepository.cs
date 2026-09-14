using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class FavoriteRepository(ApplicationDbContext dbContext) : IFavoriteRepository
{
    public async Task<bool> ExistsForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Favorites
            .AsNoTracking()
            .AnyAsync(
                favorite => favorite.UserId == userId && favorite.MovieId == movieId,
                cancellationToken);
    }

    public async Task<bool> ExistsForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Favorites
            .AsNoTracking()
            .AnyAsync(
                favorite => favorite.UserId == userId && favorite.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetFavoritedMovieIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var favoritedIds = await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite =>
                favorite.UserId == userId &&
                favorite.MovieId.HasValue &&
                movieIds.Contains(favorite.MovieId.Value))
            .Select(favorite => favorite.MovieId!.Value)
            .ToListAsync(cancellationToken);

        return favoritedIds.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetFavoritedTvShowIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var favoritedIds = await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite =>
                favorite.UserId == userId &&
                favorite.TvShowId.HasValue &&
                tvShowIds.Contains(favorite.TvShowId.Value))
            .Select(favorite => favorite.TvShowId!.Value)
            .ToListAsync(cancellationToken);

        return favoritedIds.ToHashSet();
    }

    public async Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default)
    {
        favorite.ValidateInvariants();

        try
        {
            dbContext.Favorites.Add(favorite);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            return false;
        }
    }

    public async Task<bool> RemoveForMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await dbContext.Favorites
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.MovieId == movieId,
                cancellationToken);

        if (favorite is null)
        {
            return false;
        }

        dbContext.Favorites.Remove(favorite);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await dbContext.Favorites
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.TvShowId == tvShowId,
                cancellationToken);

        if (favorite is null)
        {
            return false;
        }

        dbContext.Favorites.Remove(favorite);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<Favorite> Favorites, int TotalCount)> GetUserFavoritesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var favorites = await query
            .Include(favorite => favorite.Movie)
            .Include(favorite => favorite.TvShow)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (favorites, totalCount);
    }
}
