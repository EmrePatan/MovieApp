using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class EpisodeRepository(ApplicationDbContext dbContext) : IEpisodeRepository
{
    public async Task<Episode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .Include(episode => episode.Season)
            .FirstOrDefaultAsync(episode => episode.Id == id, cancellationToken);
    }

    public async Task<int> CountByTvShowIdAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .CountAsync(episode => episode.Season.TvShowId == tvShowId, cancellationToken);
    }

    public async Task<int> CountByTvShowIdAndSeasonNumberAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .CountAsync(
                episode => episode.Season.TvShowId == tvShowId && episode.Season.SeasonNumber == seasonNumber,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SeasonEpisodeCountResult>> GetEpisodeCountsBySeasonAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episode.Season.TvShowId == tvShowId)
            .GroupBy(episode => episode.Season.SeasonNumber)
            .Select(group => new SeasonEpisodeCountResult(group.Key, group.Count()))
            .OrderBy(result => result.SeasonNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Episode?> GetFirstUnwatchedForTvShowAsync(
        Guid tvShowId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .Include(episode => episode.Season)
            .Where(episode => episode.Season.TvShowId == tvShowId)
            .Where(episode => !dbContext.WatchedEpisodes.Any(
                watchedEpisode =>
                    watchedEpisode.UserId == userId &&
                    watchedEpisode.EpisodeId == episode.Id))
            .OrderBy(episode => episode.Season.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Episode?> GetFirstUnwatchedForSeasonAsync(
        Guid tvShowId,
        int seasonNumber,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episode.Season.TvShowId == tvShowId && episode.Season.SeasonNumber == seasonNumber)
            .Where(episode => !dbContext.WatchedEpisodes.Any(
                watchedEpisode =>
                    watchedEpisode.UserId == userId &&
                    watchedEpisode.EpisodeId == episode.Id))
            .OrderBy(episode => episode.EpisodeNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Episode?> GetBySeasonIdAndEpisodeNumberAsync(
        Guid seasonId,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                episode => episode.SeasonId == seasonId && episode.EpisodeNumber == episodeNumber,
                cancellationToken);
    }

    public async Task<Episode> UpsertFromProviderAsync(
        Guid seasonId,
        EpisodeProviderDetails details,
        CancellationToken cancellationToken = default)
    {
        var episode = await dbContext.Episodes
            .FirstOrDefaultAsync(
                existingEpisode =>
                    existingEpisode.SeasonId == seasonId &&
                    existingEpisode.EpisodeNumber == details.EpisodeNumber,
                cancellationToken);

        var utcNow = DateTime.UtcNow;

        if (episode is null)
        {
            episode = new Episode
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                CreatedAt = utcNow
            };

            dbContext.Episodes.Add(episode);
        }

        episode.TmdbId = details.TmdbId;
        episode.TvdbId = details.TvdbId;
        episode.ImdbId = details.ImdbId;
        episode.EpisodeNumber = details.EpisodeNumber;
        episode.Name = details.Name;
        episode.Overview = details.Overview;
        episode.AirDate = details.AirDate;
        episode.RuntimeMinutes = details.RuntimeMinutes;
        episode.StillPath = details.StillPath;
        episode.VoteAverage = details.VoteAverage;
        episode.VoteCount = details.VoteCount;
        episode.UpdatedAt = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return episode;
    }

    public async Task<IReadOnlyList<Guid>> GetEpisodeIdsBelongingToTvShowAsync(
        Guid tvShowId,
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken = default)
    {
        if (episodeIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episodeIds.Contains(episode.Id) && episode.Season.TvShowId == tvShowId)
            .Select(episode => episode.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowUpToEpisodeAsync(
        Guid tvShowId,
        Guid targetEpisodeId,
        CancellationToken cancellationToken = default)
    {
        var targetEpisode = await dbContext.Episodes
            .AsNoTracking()
            .Include(episode => episode.Season)
            .FirstOrDefaultAsync(
                episode => episode.Id == targetEpisodeId && episode.Season.TvShowId == tvShowId,
                cancellationToken);

        if (targetEpisode is null)
        {
            return [];
        }

        return await dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episode.Season.TvShowId == tvShowId)
            .Where(episode =>
                episode.Season.SeasonNumber < targetEpisode.Season.SeasonNumber ||
                (episode.Season.SeasonNumber == targetEpisode.Season.SeasonNumber &&
                 episode.EpisodeNumber <= targetEpisode.EpisodeNumber))
            .OrderBy(episode => episode.Season.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .Select(episode => episode.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetEpisodeIdsForSeasonAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .Where(episode =>
                episode.Season.TvShowId == tvShowId &&
                episode.Season.SeasonNumber == seasonNumber)
            .OrderBy(episode => episode.EpisodeNumber)
            .Select(episode => episode.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episode.Season.TvShowId == tvShowId)
            .OrderBy(episode => episode.Season.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .Select(episode => episode.Id)
            .ToListAsync(cancellationToken);
    }
}
