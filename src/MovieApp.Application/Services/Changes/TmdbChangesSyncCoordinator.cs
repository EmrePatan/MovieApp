using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.Application.Services.Changes;

internal static class TmdbChangesSyncCoordinator
{
    internal static async Task<TmdbChangesSyncResult> SyncAsync(
        string checkpointKey,
        Func<DateOnly, DateOnly, int, CancellationToken, Task<TmdbChangesPageResult>> getChangesPageAsync,
        Func<IReadOnlyCollection<int>, CancellationToken, Task<CatalogChangesRefreshMaps>> resolveRefreshMapsAsync,
        Func<Guid, DateOnly, DateOnly, CancellationToken, Task<TmdbChangesTargetRefreshResult>> refreshTargetAsync,
        ITmdbTvChangesSyncCheckpointRepository checkpointRepository,
        ILogger logger,
        string mediaTypeLabel,
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var syncInstant = utcNow ?? DateTime.UtcNow;
        var targetDate = TmdbTvChangesWindowPlanner.DetermineTargetDate(syncInstant);

        var lastCompletedEndDate = await checkpointRepository.GetLastCompletedEndDateAsync(
            checkpointKey,
            cancellationToken);

        var windowStart = TmdbTvChangesWindowPlanner.DetermineNextWindowStart(
            lastCompletedEndDate,
            targetDate);

        if (windowStart is null || windowStart.Value > targetDate)
        {
            return new TmdbChangesSyncResult(0, 0, 0, 0, 0, 0, lastCompletedEndDate);
        }

        var chunks = TmdbTvChangesWindowPlanner.BuildChunks(windowStart.Value, targetDate);
        var totalChangedIds = 0;
        var totalRelevantTargets = 0;
        var totalRefreshed = 0;
        var totalSkipped = 0;
        var totalFailed = 0;
        var totalExistingUserMatches = 0;
        var totalDiscoveryMatches = 0;
        var totalDiscoveryOnlyMatches = 0;
        DateOnly? latestCompletedEndDate = lastCompletedEndDate;

        foreach (var chunk in chunks)
        {
            LogMemorySnapshot(logger, mediaTypeLabel, chunk.Start, chunk.End, "BeforeFetch", 0, 0);

            var changedTmdbIds = await FetchAllChangedTmdbIdsAsync(
                getChangesPageAsync,
                chunk.Start,
                chunk.End,
                cancellationToken);

            totalChangedIds += changedTmdbIds.Count;
            LogMemorySnapshot(logger, mediaTypeLabel, chunk.Start, chunk.End, "AfterFetch", changedTmdbIds.Count, 0);

            var refreshMaps = await resolveRefreshMapsAsync(changedTmdbIds, cancellationToken);
            var relevantTargets = refreshMaps.UnionByTmdbId;

            var existingUserMatches = changedTmdbIds.Count(
                tmdbId => refreshMaps.UserRelevantByTmdbId.ContainsKey(tmdbId));
            var discoveryMatches = changedTmdbIds.Count(
                tmdbId => refreshMaps.DiscoveryRelevantByTmdbId.ContainsKey(tmdbId));
            var discoveryOnlyMatches = changedTmdbIds.Count(
                tmdbId => refreshMaps.DiscoveryRelevantByTmdbId.ContainsKey(tmdbId)
                          && !refreshMaps.UserRelevantByTmdbId.ContainsKey(tmdbId));

            totalExistingUserMatches += existingUserMatches;
            totalDiscoveryMatches += discoveryMatches;
            totalDiscoveryOnlyMatches += discoveryOnlyMatches;

            var targetIds = changedTmdbIds
                .Where(relevantTargets.ContainsKey)
                .Select(tmdbId => relevantTargets[tmdbId])
                .Distinct()
                .ToList();

            totalRelevantTargets += targetIds.Count;
            LogMemorySnapshot(logger, mediaTypeLabel, chunk.Start, chunk.End, "AfterResolve", changedTmdbIds.Count, targetIds.Count);

            var chunkRefreshed = 0;
            var chunkSkipped = 0;
            var chunkFailed = 0;

            foreach (var targetId in targetIds)
            {
                try
                {
                    var refreshResult = await refreshTargetAsync(
                        targetId,
                        chunk.End,
                        chunk.End,
                        cancellationToken);

                    switch (refreshResult.Outcome)
                    {
                        case TmdbChangesTargetRefreshOutcome.Refreshed:
                            chunkRefreshed++;
                            break;
                        case TmdbChangesTargetRefreshOutcome.SkippedUnavailable:
                        case TmdbChangesTargetRefreshOutcome.SkippedNotFound:
                            chunkSkipped++;
                            TmdbChangesSyncLogMessages.LogSkippedTargetRefresh(
                                logger,
                                mediaTypeLabel,
                                targetId,
                                refreshResult.Outcome);
                            break;
                        default:
                            chunkFailed++;
                            TmdbChangesSyncLogMessages.LogFailedTargetRefresh(
                                logger,
                                mediaTypeLabel,
                                targetId);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    chunkFailed++;
                    TmdbChangesSyncLogMessages.LogFailedTargetRefresh(
                        logger,
                        exception,
                        mediaTypeLabel,
                        targetId);
                }
            }

            totalRefreshed += chunkRefreshed;
            totalSkipped += chunkSkipped;
            totalFailed += chunkFailed;
            LogMemorySnapshot(logger, mediaTypeLabel, chunk.Start, chunk.End, "AfterRefresh", changedTmdbIds.Count, targetIds.Count);

            TmdbChangesSyncLogMessages.LogChunkProcessed(
                logger,
                mediaTypeLabel,
                chunk.Start,
                chunk.End,
                changedTmdbIds.Count,
                existingUserMatches,
                discoveryMatches,
                discoveryOnlyMatches,
                targetIds.Count,
                chunkRefreshed,
                chunkSkipped,
                chunkFailed);

            await checkpointRepository.CompleteWindowAsync(
                checkpointKey,
                chunk.End,
                syncInstant,
                cancellationToken);

            latestCompletedEndDate = chunk.End;
        }

        TmdbChangesSyncLogMessages.LogRunCompleted(
            logger,
            mediaTypeLabel,
            totalChangedIds,
            totalExistingUserMatches,
            totalDiscoveryMatches,
            totalDiscoveryOnlyMatches,
            totalRelevantTargets,
            totalRefreshed,
            totalSkipped,
            totalFailed);

        return new TmdbChangesSyncResult(
            chunks.Count,
            totalChangedIds,
            totalRelevantTargets,
            totalRefreshed,
            totalSkipped,
            totalFailed,
            latestCompletedEndDate,
            totalExistingUserMatches,
            totalDiscoveryMatches,
            totalDiscoveryOnlyMatches);
    }

    private static void LogMemorySnapshot(
        ILogger logger,
        string mediaTypeLabel,
        DateOnly startDate,
        DateOnly endDate,
        string stage,
        int changedIds,
        int targetIds)
    {
        using var process = Process.GetCurrentProcess();
        var managedHeapMb = GC.GetTotalMemory(forceFullCollection: false) / 1024d / 1024d;
        var workingSetMb = process.WorkingSet64 / 1024d / 1024d;

        logger.LogInformation(
            "TMDB changes sync memory: mediaType={MediaType} window={StartDate}..{EndDate} stage={Stage} changedIds={ChangedIds} targetIds={TargetIds} managedHeapMb={ManagedHeapMb:F1} workingSetMb={WorkingSetMb:F1}",
            mediaTypeLabel,
            startDate,
            endDate,
            stage,
            changedIds,
            targetIds,
            managedHeapMb,
            workingSetMb);
    }

    private static async Task<HashSet<int>> FetchAllChangedTmdbIdsAsync(
        Func<DateOnly, DateOnly, int, CancellationToken, Task<TmdbChangesPageResult>> getChangesPageAsync,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var changedTmdbIds = new HashSet<int>();
        var page = 1;
        var totalPages = 1;

        while (page <= totalPages)
        {
            var pageResult = await getChangesPageAsync(startDate, endDate, page, cancellationToken);

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
