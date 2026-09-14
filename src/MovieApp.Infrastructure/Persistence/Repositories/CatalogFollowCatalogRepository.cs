using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogFollowCatalogRepository(ApplicationDbContext dbContext)
    : ICatalogFollowCatalogRepository
{
    public async Task<(IReadOnlyList<CatalogFollowItemResult> Items, int TotalCount)> GetFollowingCatalogAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from follow in dbContext.CatalogFollows.AsNoTracking()
            where follow.UserId == userId
            join movie in dbContext.Movies.AsNoTracking()
                on follow.ContentId equals movie.Id into movieJoin
            from movie in movieJoin.DefaultIfEmpty()
            join tvShow in dbContext.TvShows.AsNoTracking()
                on follow.ContentId equals tvShow.Id into tvJoin
            from tvShow in tvJoin.DefaultIfEmpty()
            where (follow.ContentType == CatalogContentType.Movie && movie != null)
                  || (follow.ContentType == CatalogContentType.Tv && tvShow != null)
            orderby follow.CreatedAt descending
            select new CatalogFollowItemResult(
                follow.ContentId,
                follow.ContentType,
                follow.ContentType == CatalogContentType.Movie ? movie!.Title : tvShow!.Title,
                follow.ContentType == CatalogContentType.Movie ? movie!.PosterPath : tvShow!.PosterPath,
                follow.ContentType == CatalogContentType.Movie ? movie!.ReleaseDate : tvShow!.FirstAirDate,
                follow.NotifyMovieRelease,
                follow.NotifyNewSeasons,
                follow.NotifyNewEpisodes,
                follow.BaselineEstablishedAtUtc != null,
                follow.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetUpcomingCatalogAsync(
        Guid? userId,
        int page,
        int pageSize,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var movieQuery =
            from movie in dbContext.Movies.AsNoTracking()
            where movie.ReleaseDate != null && movie.ReleaseDate > today
            select new CatalogUpcomingItemResult(
                movie.Id,
                CatalogContentType.Movie,
                movie.Title,
                movie.PosterPath,
                movie.ReleaseDate!.Value,
                false);

        var tvQuery =
            from tvShow in dbContext.TvShows.AsNoTracking()
            where tvShow.FirstAirDate != null && tvShow.FirstAirDate > today
            select new CatalogUpcomingItemResult(
                tvShow.Id,
                CatalogContentType.Tv,
                tvShow.Title,
                tvShow.PosterPath,
                tvShow.FirstAirDate!.Value,
                false);

        var combinedQuery = movieQuery.Concat(tvQuery);
        var orderedQuery = combinedQuery.OrderBy(item => item.ReleaseDate);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (!userId.HasValue || items.Count == 0)
        {
            return (items, totalCount);
        }

        var contentKeys = items
            .Select(item => new { item.ContentId, item.ContentType })
            .ToList();

        var followedKeys = await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == userId.Value)
            .Where(follow => contentKeys.Any(key =>
                key.ContentId == follow.ContentId && key.ContentType == follow.ContentType))
            .Select(follow => new { follow.ContentId, follow.ContentType })
            .ToListAsync(cancellationToken);

        var followedSet = followedKeys
            .Select(key => (key.ContentId, key.ContentType))
            .ToHashSet();

        items = items
            .Select(item => item with
            {
                IsFollowed = followedSet.Contains((item.ContentId, item.ContentType))
            })
            .ToList();

        return (items, totalCount);
    }
}
