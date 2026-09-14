using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class WatchedEpisodeRepository(ApplicationDbContext dbContext) : IWatchedEpisodeRepository
{
    public async Task<WatchedEpisode?> GetByUserAndEpisodeAsync(
        Guid userId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedEpisodes
            .FirstOrDefaultAsync(
                watchedEpisode => watchedEpisode.UserId == userId && watchedEpisode.EpisodeId == episodeId,
                cancellationToken);
    }

    public async Task<(WatchedEpisode Entity, bool Created)> UpsertAsync(
        WatchedEpisode watchedEpisode,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.WatchedEpisodes
            .FirstOrDefaultAsync(
                item => item.UserId == watchedEpisode.UserId && item.EpisodeId == watchedEpisode.EpisodeId,
                cancellationToken);

        if (existing is not null)
        {
            existing.UpdateWatchedAt(watchedEpisode.WatchedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (existing, false);
        }

        try
        {
            dbContext.WatchedEpisodes.Add(watchedEpisode);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (watchedEpisode, true);
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            var raced = await dbContext.WatchedEpisodes
                .FirstAsync(
                    item => item.UserId == watchedEpisode.UserId && item.EpisodeId == watchedEpisode.EpisodeId,
                    cancellationToken);

            raced.UpdateWatchedAt(watchedEpisode.WatchedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (raced, false);
        }
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var watchedEpisode = await dbContext.WatchedEpisodes
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.EpisodeId == episodeId,
                cancellationToken);

        if (watchedEpisode is null)
        {
            return false;
        }

        dbContext.WatchedEpisodes.Remove(watchedEpisode);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<WatchedEpisode> Items, int TotalCount)> GetUserWatchedEpisodesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(watchedEpisode => watchedEpisode.Episode)
            .ThenInclude(episode => episode.Season)
            .ThenInclude(season => season.TvShow)
            .OrderByDescending(watchedEpisode => watchedEpisode.WatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<int> CountWatchedForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .CountAsync(
                watchedEpisode => watchedEpisode.Episode.Season.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<int> CountWatchedForSeasonAsync(
        Guid userId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .CountAsync(
                watchedEpisode => watchedEpisode.Episode.SeasonId == seasonId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SeasonEpisodeCountResult>> GetWatchedEpisodeCountsBySeasonAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.Episode.Season.TvShowId == tvShowId)
            .GroupBy(watchedEpisode => watchedEpisode.Episode.Season.SeasonNumber)
            .Select(group => new
            {
                SeasonNumber = group.Key,
                EpisodeCount = group.Count(),
            })
            .OrderBy(row => row.SeasonNumber)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SeasonEpisodeCountResult(row.SeasonNumber, row.EpisodeCount))
            .ToList();
    }

    public async Task<IReadOnlyList<(
        Guid EpisodeId,
        Guid TvShowId,
        Guid SeasonId,
        string TvShowTitle,
        int SeasonNumber,
        int EpisodeNumber,
        string? EpisodeTitle,
        DateTime WatchedAt)>> GetRecentForUserAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .OrderByDescending(watchedEpisode => watchedEpisode.WatchedAt)
            .Take(take)
            .Include(watchedEpisode => watchedEpisode.Episode)
            .ThenInclude(episode => episode.Season)
            .ThenInclude(season => season.TvShow)
            .ToListAsync(cancellationToken);

        return items
            .Select(watchedEpisode => (
                watchedEpisode.EpisodeId,
                watchedEpisode.Episode.Season.TvShowId,
                watchedEpisode.Episode.SeasonId,
                watchedEpisode.Episode.Season.TvShow.Title,
                watchedEpisode.Episode.Season.SeasonNumber,
                watchedEpisode.Episode.EpisodeNumber,
                watchedEpisode.Episode.Name,
                watchedEpisode.WatchedAt))
            .ToList();
    }

    public async Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .CountAsync(watchedEpisode => watchedEpisode.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<(
        Guid TvShowId,
        string Title,
        string? OriginalTitle,
        string? PosterUrl,
        string? BackdropUrl,
        DateOnly? FirstAirDate,
        decimal VoteAverage,
        int VoteCount,
        DateTime LastWatchedAt)>> GetContinueWatchingTvShowsAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var watchedShows = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .GroupBy(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Select(group => new
            {
                TvShowId = group.Key,
                LastWatchedAt = group.Max(watchedEpisode => watchedEpisode.WatchedAt)
            });

        var items = await watchedShows
            .Where(show => dbContext.Episodes.Any(episode =>
                episode.Season.TvShowId == show.TvShowId &&
                episode.Season.SeasonNumber >= 1 &&
                !dbContext.WatchedEpisodes.Any(watchedEpisode =>
                    watchedEpisode.UserId == userId &&
                    watchedEpisode.EpisodeId == episode.Id)))
            .OrderByDescending(show => show.LastWatchedAt)
            .Take(take)
            .Join(
                dbContext.TvShows.AsNoTracking(),
                show => show.TvShowId,
                tvShow => tvShow.Id,
                (show, tvShow) => new
                {
                    show.TvShowId,
                    tvShow.Title,
                    tvShow.OriginalTitle,
                    tvShow.PosterPath,
                    tvShow.BackdropPath,
                    tvShow.FirstAirDate,
                    tvShow.VoteAverage,
                    tvShow.VoteCount,
                    show.LastWatchedAt
                })
            .ToListAsync(cancellationToken);

        return items
            .Select(item => (
                item.TvShowId,
                item.Title,
                item.OriginalTitle,
                item.PosterPath,
                item.BackdropPath,
                item.FirstAirDate,
                item.VoteAverage,
                item.VoteCount,
                item.LastWatchedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetWatchedEpisodeIdsForSeasonAsync(
        Guid userId,
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .Where(watchedEpisode =>
                watchedEpisode.Episode.Season.TvShowId == tvShowId &&
                watchedEpisode.Episode.Season.SeasonNumber == seasonNumber)
            .Select(watchedEpisode => watchedEpisode.EpisodeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> BulkMarkWatchedAsync(
        Guid userId,
        IReadOnlyList<Guid> episodeIds,
        DateTime watchedAt,
        CancellationToken cancellationToken = default)
    {
        if (episodeIds.Count == 0)
        {
            return 0;
        }

        var distinctIds = episodeIds.Distinct().ToList();
        var existingEpisodes = await dbContext.WatchedEpisodes
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                distinctIds.Contains(watchedEpisode.EpisodeId))
            .ToListAsync(cancellationToken);

        var existingIds = existingEpisodes
            .Select(watchedEpisode => watchedEpisode.EpisodeId)
            .ToHashSet();

        foreach (var watchedEpisode in existingEpisodes)
        {
            watchedEpisode.UpdateWatchedAt(watchedAt);
        }

        foreach (var episodeId in distinctIds.Where(id => !existingIds.Contains(id)))
        {
            dbContext.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodeId, watchedAt));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return distinctIds.Count;
    }

    public async Task<int> BulkUnmarkWatchedAsync(
        Guid userId,
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken = default)
    {
        if (episodeIds.Count == 0)
        {
            return 0;
        }

        var distinctIds = episodeIds.Distinct().ToList();
        var watchedEpisodes = await dbContext.WatchedEpisodes
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                distinctIds.Contains(watchedEpisode.EpisodeId))
            .ToListAsync(cancellationToken);

        if (watchedEpisodes.Count == 0)
        {
            return 0;
        }

        dbContext.WatchedEpisodes.RemoveRange(watchedEpisodes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return watchedEpisodes.Count;
    }
}
