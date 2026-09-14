using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.TvShowChanges;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShowChanges;

public sealed class TmdbTvChangesSyncService(
    ITmdbTvChangesProvider tmdbTvChangesProvider,
    ITmdbTvChangesSyncCheckpointRepository checkpointRepository,
    IFollowedTvShowCatalogRepository followedTvShowCatalogRepository,
    ITvShowChangesTargetedRefreshService targetedRefreshService) : ITmdbTvChangesSyncService
{
    public async Task<TmdbTvChangesSyncResult> SyncAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var syncInstant = utcNow ?? DateTime.UtcNow;
        var targetDate = TmdbTvChangesWindowPlanner.DetermineTargetDate(syncInstant);
        var checkpointKey = TmdbTvChangesSyncCheckpoint.DefaultCheckpointKey;

        var lastCompletedEndDate = await checkpointRepository.GetLastCompletedEndDateAsync(
            checkpointKey,
            cancellationToken);

        var windowStart = TmdbTvChangesWindowPlanner.DetermineNextWindowStart(
            lastCompletedEndDate,
            targetDate);

        if (windowStart is null || windowStart.Value > targetDate)
        {
            return new TmdbTvChangesSyncResult(0, 0, 0, lastCompletedEndDate);
        }

        var chunks = TmdbTvChangesWindowPlanner.BuildChunks(windowStart.Value, targetDate);
        var totalChangedIds = 0;
        var totalRefreshedShows = 0;
        DateOnly? latestCompletedEndDate = lastCompletedEndDate;

        foreach (var chunk in chunks)
        {
            var changedTmdbIds = await FetchAllChangedTmdbIdsAsync(
                chunk.Start,
                chunk.End,
                cancellationToken);

            totalChangedIds += changedTmdbIds.Count;

            var followedShows = await followedTvShowCatalogRepository.GetFollowedTvShowIdsByTmdbIdAsync(
                cancellationToken);

            var tvShowIdsToRefresh = changedTmdbIds
                .Where(followedShows.ContainsKey)
                .Select(tmdbId => followedShows[tmdbId])
                .Distinct()
                .ToList();

            foreach (var tvShowId in tvShowIdsToRefresh)
            {
                await targetedRefreshService.RefreshFollowedShowAsync(
                    tvShowId,
                    chunk.End,
                    chunk.End,
                    cancellationToken);
            }

            totalRefreshedShows += tvShowIdsToRefresh.Count;

            await checkpointRepository.CompleteWindowAsync(
                checkpointKey,
                chunk.End,
                syncInstant,
                cancellationToken);

            latestCompletedEndDate = chunk.End;
        }

        return new TmdbTvChangesSyncResult(
            chunks.Count,
            totalChangedIds,
            totalRefreshedShows,
            latestCompletedEndDate);
    }

    private async Task<HashSet<int>> FetchAllChangedTmdbIdsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var changedTmdbIds = new HashSet<int>();
        var page = 1;
        var totalPages = 1;

        while (page <= totalPages)
        {
            var pageResult = await tmdbTvChangesProvider.GetTvChangesPageAsync(
                startDate,
                endDate,
                page,
                cancellationToken);

            foreach (var tmdbId in pageResult.ChangedTmdbIds)
            {
                changedTmdbIds.Add(tmdbId);
            }

            totalPages = pageResult.TotalPages;
            page++;
        }

        return changedTmdbIds;
    }
}
