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
        string region,
        CancellationToken cancellationToken = default)
    {
        var movieQuery =
            from movie in dbContext.Movies.AsNoTracking()
            join regionalRelease in dbContext.MovieRegionalReleases.AsNoTracking()
                on new { MovieId = movie.Id, Region = region }
                equals new { regionalRelease.MovieId, regionalRelease.Region }
                into regionalJoin
            from regionalRelease in regionalJoin.DefaultIfEmpty()
            let effectiveReleaseDate = regionalRelease != null
                ? regionalRelease.EffectiveReleaseDate
                : movie.ReleaseDate
            where effectiveReleaseDate != null && effectiveReleaseDate > today
            select new UpcomingCatalogRow(
                movie.Id,
                CatalogContentType.Movie,
                CatalogUpcomingKind.MovieRelease,
                movie.Title,
                movie.PosterPath,
                effectiveReleaseDate!.Value,
                null,
                null,
                null,
                null);

        var tvQuery =
            from tvShow in dbContext.TvShows.AsNoTracking()
            where tvShow.FirstAirDate != null && tvShow.FirstAirDate > today
            select new UpcomingCatalogRow(
                tvShow.Id,
                CatalogContentType.Tv,
                CatalogUpcomingKind.TvShowPremiere,
                tvShow.Title,
                tvShow.PosterPath,
                tvShow.FirstAirDate!.Value,
                null,
                null,
                null,
                null);

        var movieCount = await movieQuery.CountAsync(cancellationToken);
        var tvCount = await tvQuery.CountAsync(cancellationToken);

        IReadOnlyList<UpcomingCatalogRow> episodeRows = [];
        var episodeCount = 0;

        if (userId.HasValue)
        {
            episodeRows = await GetFollowedTvNextEpisodeRowsAsync(userId.Value, today, cancellationToken);
            episodeCount = episodeRows.Count;
        }

        var totalCount = movieCount + tvCount + episodeCount;
        var fetchCount = Math.Min(totalCount, ((page - 1) * pageSize) + pageSize);
        var skip = (page - 1) * pageSize;

        var movieRows = fetchCount == 0
            ? []
            : await movieQuery
                .OrderBy(item => item.ReleaseDate)
                .ThenBy(item => item.ContentId)
                .Take(fetchCount)
                .ToListAsync(cancellationToken);

        var tvRows = fetchCount == 0
            ? []
            : await tvQuery
                .OrderBy(item => item.ReleaseDate)
                .ThenBy(item => item.ContentId)
                .Take(fetchCount)
                .ToListAsync(cancellationToken);

        var orderedEpisodeRows = episodeRows
            .OrderBy(item => item.ReleaseDate)
            .ThenBy(item => item.ContentId)
            .Take(fetchCount)
            .ToList();

        var rows = movieRows
            .Concat(tvRows)
            .Concat(orderedEpisodeRows)
            .OrderBy(item => item.ReleaseDate)
            .ThenBy(item => item.UpcomingKind)
            .ThenBy(item => item.ContentType)
            .ThenBy(item => item.ContentId)
            .ThenBy(item => item.EpisodeId)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        var items = rows
            .Select(row => new CatalogUpcomingItemResult(
                row.ContentId,
                row.ContentType,
                row.UpcomingKind,
                row.Title,
                row.PosterPath,
                row.ReleaseDate,
                row.UpcomingKind == CatalogUpcomingKind.TvEpisode,
                row.EpisodeId,
                row.SeasonNumber,
                row.EpisodeNumber,
                row.EpisodeName))
            .ToList();

        if (!userId.HasValue || items.Count == 0)
        {
            return (items, totalCount);
        }

        var movieIds = items
            .Where(item => item.ContentType == CatalogContentType.Movie)
            .Select(item => item.ContentId)
            .ToList();
        var tvIds = items
            .Where(item => item.ContentType == CatalogContentType.Tv && item.UpcomingKind != CatalogUpcomingKind.TvEpisode)
            .Select(item => item.ContentId)
            .ToList();

        var followedKeys = await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow =>
                follow.UserId == userId.Value &&
                ((follow.ContentType == CatalogContentType.Movie && movieIds.Contains(follow.ContentId)) ||
                 (follow.ContentType == CatalogContentType.Tv && tvIds.Contains(follow.ContentId))))
            .Select(follow => new { follow.ContentId, follow.ContentType })
            .ToListAsync(cancellationToken);

        var followedSet = followedKeys
            .Select(key => (key.ContentId, key.ContentType))
            .ToHashSet();

        items = items
            .Select(item => item with
            {
                IsFollowed = item.UpcomingKind == CatalogUpcomingKind.TvEpisode ||
                             followedSet.Contains((item.ContentId, item.ContentType))
            })
            .ToList();

        return (items, totalCount);
    }

    private async Task<IReadOnlyList<UpcomingCatalogRow>> GetFollowedTvNextEpisodeRowsAsync(
        Guid userId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var futureEpisodes = await (
            from follow in dbContext.CatalogFollows.AsNoTracking()
            where follow.UserId == userId && follow.ContentType == CatalogContentType.Tv
            join tvShow in dbContext.TvShows.AsNoTracking() on follow.ContentId equals tvShow.Id
            join season in dbContext.Seasons.AsNoTracking() on tvShow.Id equals season.TvShowId
            join episode in dbContext.Episodes.AsNoTracking() on season.Id equals episode.SeasonId
            where episode.AirDate != null && episode.AirDate > today
            select new UpcomingCatalogRow(
                tvShow.Id,
                CatalogContentType.Tv,
                CatalogUpcomingKind.TvEpisode,
                tvShow.Title,
                tvShow.PosterPath,
                episode.AirDate!.Value,
                episode.Id,
                season.SeasonNumber,
                episode.EpisodeNumber,
                episode.Name))
            .ToListAsync(cancellationToken);

        return futureEpisodes
            .GroupBy(row => row.ContentId)
            .Select(group => group
                .OrderBy(row => row.ReleaseDate)
                .ThenBy(row => row.SeasonNumber)
                .ThenBy(row => row.EpisodeNumber)
                .ThenBy(row => row.EpisodeId)
                .First())
            .ToList();
    }

    private sealed record UpcomingCatalogRow(
        Guid ContentId,
        CatalogContentType ContentType,
        CatalogUpcomingKind UpcomingKind,
        string Title,
        string? PosterPath,
        DateOnly ReleaseDate,
        Guid? EpisodeId,
        int? SeasonNumber,
        int? EpisodeNumber,
        string? EpisodeName);
}
