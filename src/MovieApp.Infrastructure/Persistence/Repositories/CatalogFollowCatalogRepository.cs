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
            select new
            {
                ContentId = movie.Id,
                ContentType = CatalogContentType.Movie,
                Title = movie.Title,
                PosterPath = movie.PosterPath,
                ReleaseDate = movie.ReleaseDate!.Value
            };

        var tvQuery =
            from tvShow in dbContext.TvShows.AsNoTracking()
            where tvShow.FirstAirDate != null && tvShow.FirstAirDate > today
            select new
            {
                ContentId = tvShow.Id,
                ContentType = CatalogContentType.Tv,
                Title = tvShow.Title,
                PosterPath = tvShow.PosterPath,
                ReleaseDate = tvShow.FirstAirDate!.Value
            };

        var orderedQuery = movieQuery
            .Concat(tvQuery)
            .OrderBy(item => item.ReleaseDate);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var rows = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new CatalogUpcomingItemResult(
                row.ContentId,
                row.ContentType,
                row.Title,
                row.PosterPath,
                row.ReleaseDate,
                false))
            .ToList();

        if (!userId.HasValue || items.Count == 0)
        {
            return (items, totalCount);
        }

        var contentIds = items
            .Select(item => item.ContentId)
            .ToList();

        var followedKeys = await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == userId.Value && contentIds.Contains(follow.ContentId))
            .Select(follow => new { follow.ContentId, follow.ContentType })
            .ToListAsync(cancellationToken);

        var pageKeySet = items
            .Select(item => (item.ContentId, item.ContentType))
            .ToHashSet();

        var followedSet = followedKeys
            .Where(key => pageKeySet.Contains((key.ContentId, key.ContentType)))
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
