using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class WatchlistItemRepository(ApplicationDbContext dbContext) : IWatchlistItemRepository
{
    public async Task<bool> ExistsForMovieAsync(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchlistItems
            .AsNoTracking()
            .AnyAsync(
                item => item.WatchlistId == watchlistId && item.MovieId == movieId,
                cancellationToken);
    }

    public async Task<bool> ExistsForTvShowAsync(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchlistItems
            .AsNoTracking()
            .AnyAsync(
                item => item.WatchlistId == watchlistId && item.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<bool> TryAddAsync(WatchlistItem item, CancellationToken cancellationToken = default)
    {
        item.ValidateInvariants();

        try
        {
            dbContext.WatchlistItems.Add(item);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            return false;
        }
    }

    public async Task<bool> RemoveForMovieAsync(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.WatchlistItems
            .FirstOrDefaultAsync(
                watchlistItem => watchlistItem.WatchlistId == watchlistId && watchlistItem.MovieId == movieId,
                cancellationToken);

        if (item is null)
        {
            return false;
        }

        dbContext.WatchlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveForTvShowAsync(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.WatchlistItems
            .FirstOrDefaultAsync(
                watchlistItem => watchlistItem.WatchlistId == watchlistId && watchlistItem.TvShowId == tvShowId,
                cancellationToken);

        if (item is null)
        {
            return false;
        }

        dbContext.WatchlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<WatchlistItem> Items, int TotalCount)> GetItemsAsync(
        Guid watchlistId,
        SearchContentType mediaType,
        WatchlistItemsSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.WatchlistId == watchlistId);

        query = mediaType switch
        {
            SearchContentType.Movie => query.Where(item => item.MovieId != null),
            SearchContentType.Tv => query.Where(item => item.TvShowId != null),
            _ => query,
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var hydratedQuery = query
            .Include(item => item.Movie)
            .Include(item => item.TvShow);

        var orderedQuery = sort switch
        {
            WatchlistItemsSort.TitleAsc => hydratedQuery
                .OrderBy(item => item.Movie != null ? item.Movie!.Title : item.TvShow!.Title),
            WatchlistItemsSort.RatingDesc => hydratedQuery
                .OrderByDescending(item => item.Movie != null ? item.Movie!.VoteAverage : item.TvShow!.VoteAverage),
            _ => hydratedQuery.OrderByDescending(item => item.CreatedAt),
        };

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<WatchlistItem>> GetAllItemsAsync(
        Guid watchlistId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.WatchlistId == watchlistId)
            .Include(item => item.Movie)
            .Include(item => item.TvShow)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetItemCountAsync(Guid watchlistId, CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchlistItems
            .AsNoTracking()
            .CountAsync(item => item.WatchlistId == watchlistId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetItemCountsByWatchlistIdsAsync(
        IReadOnlyCollection<Guid> watchlistIds,
        CancellationToken cancellationToken = default)
    {
        if (watchlistIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => watchlistIds.Contains(item.WatchlistId))
            .GroupBy(item => item.WatchlistId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId && item.MovieId == movieId)
            .Select(item => item.WatchlistId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId && item.TvShowId == tvShowId)
            .Select(item => item.WatchlistId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
