using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.Application.Services.Changes;

internal static partial class TmdbChangesSyncCoordinator
{
    // Movie and TV sync jobs share this process-wide gate so parallel refreshes never
    // overwhelm the small production database or TMDB rate limits.
    private static readonly SemaphoreSlim TargetRefreshGate = new(2, 2);

    internal const int MaxRetries = 5;

    private const int RetriesPerRun = 50;

    // More failures than this in one window look like an outage, not a few broken titles.
    private const int MaxDeferredPerWindow = 25;

    // A window where nothing worked may still move on when only this many titles failed.
    private const int MaxDeferredWithoutSuccess = 3;

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

        await RetryFailedTargetsAsync(
            checkpointKey,
            refreshTargetAsync,
            checkpointRepository,
            logger,
            mediaTypeLabel,
            syncInstant,
            cancellationToken);

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
        var windowsProcessed = 0;
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

            var outcome = await RefreshTargetsAsync(
                targetIds.Select(targetId => (targetId, chunk.End)).ToList(),
                refreshTargetAsync,
                logger,
                mediaTypeLabel,
                cancellationToken);
            var chunkRefreshed = outcome.Refreshed;
            var chunkSkipped = outcome.Skipped;
            var chunkFailed = outcome.FailedIds.Count;

            await checkpointRepository.ResolveFailuresAsync(
                checkpointKey,
                outcome.SucceededIds,
                cancellationToken);

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

            // A few broken titles are kept for retry so they cannot hold the window forever.
            // During an outage the window stays, and the whole range is read again later.
            if (chunkFailed > 0)
            {
                var canDefer = chunkFailed <= MaxDeferredPerWindow
                               && (chunkRefreshed + chunkSkipped > 0 || chunkFailed <= MaxDeferredWithoutSuccess);
                if (!canDefer)
                {
                    break;
                }

                await checkpointRepository.RecordFailuresAsync(
                    checkpointKey,
                    outcome.FailedIds,
                    chunk.End,
                    MaxRetries,
                    syncInstant,
                    cancellationToken);
            }

            await checkpointRepository.CompleteWindowAsync(
                checkpointKey,
                chunk.End,
                syncInstant,
                cancellationToken);

            windowsProcessed++;
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
            windowsProcessed,
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

    private static async Task RetryFailedTargetsAsync(
        string checkpointKey,
        Func<Guid, DateOnly, DateOnly, CancellationToken, Task<TmdbChangesTargetRefreshResult>> refreshTargetAsync,
        ITmdbTvChangesSyncCheckpointRepository checkpointRepository,
        ILogger logger,
        string mediaTypeLabel,
        DateTime syncInstant,
        CancellationToken cancellationToken)
    {
        var pending = await checkpointRepository.GetRetryableFailuresAsync(
            checkpointKey,
            MaxRetries,
            RetriesPerRun,
            cancellationToken);

        if (pending.Count > 0)
        {
            var outcome = await RefreshTargetsAsync(
                pending.Select(failure => (failure.TargetId, failure.ChangeDate)).ToList(),
                refreshTargetAsync,
                logger,
                mediaTypeLabel,
                cancellationToken);

            await checkpointRepository.ResolveFailuresAsync(
                checkpointKey,
                outcome.SucceededIds,
                cancellationToken);

            // When every retry fails the cause is probably an outage, so no attempt is spent.
            if (outcome.SucceededIds.Count > 0 || outcome.FailedIds.Count <= MaxDeferredWithoutSuccess)
            {
                foreach (var targetId in outcome.FailedIds)
                {
                    await checkpointRepository.RecordRetryFailureAsync(
                        checkpointKey,
                        targetId,
                        syncInstant,
                        cancellationToken);
                }
            }
        }

        var exhausted = await checkpointRepository.CountExhaustedFailuresAsync(
            checkpointKey,
            MaxRetries,
            cancellationToken);
        if (exhausted > 0)
        {
            LogExhaustedTargets(logger, mediaTypeLabel, exhausted);
        }
    }

    private static async Task<RefreshOutcome> RefreshTargetsAsync(
        IReadOnlyList<(Guid TargetId, DateOnly ChangeDate)> targets,
        Func<Guid, DateOnly, DateOnly, CancellationToken, Task<TmdbChangesTargetRefreshResult>> refreshTargetAsync,
        ILogger logger,
        string mediaTypeLabel,
        CancellationToken cancellationToken)
    {
        var refreshed = 0;
        var skipped = 0;
        var succeededIds = new ConcurrentBag<Guid>();
        var failedIds = new ConcurrentBag<Guid>();

        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 2,
                CancellationToken = cancellationToken
            },
            async (target, token) =>
            {
                await TargetRefreshGate.WaitAsync(token);
                try
                {
                    var refreshResult = await refreshTargetAsync(
                        target.TargetId,
                        target.ChangeDate,
                        target.ChangeDate,
                        token);

                    switch (refreshResult.Outcome)
                    {
                        case TmdbChangesTargetRefreshOutcome.Refreshed:
                            Interlocked.Increment(ref refreshed);
                            succeededIds.Add(target.TargetId);
                            break;
                        case TmdbChangesTargetRefreshOutcome.SkippedUnavailable:
                        case TmdbChangesTargetRefreshOutcome.SkippedNotFound:
                            Interlocked.Increment(ref skipped);
                            succeededIds.Add(target.TargetId);
                            TmdbChangesSyncLogMessages.LogSkippedTargetRefresh(
                                logger,
                                mediaTypeLabel,
                                target.TargetId,
                                refreshResult.Outcome);
                            break;
                        default:
                            failedIds.Add(target.TargetId);
                            TmdbChangesSyncLogMessages.LogFailedTargetRefresh(
                                logger,
                                mediaTypeLabel,
                                target.TargetId);
                            break;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failedIds.Add(target.TargetId);
                    TmdbChangesSyncLogMessages.LogFailedTargetRefresh(
                        logger,
                        exception,
                        mediaTypeLabel,
                        target.TargetId);
                }
                finally
                {
                    TargetRefreshGate.Release();
                }
            });

        return new RefreshOutcome(refreshed, skipped, succeededIds.ToArray(), failedIds.ToArray());
    }

    private sealed record RefreshOutcome(
        int Refreshed,
        int Skipped,
        IReadOnlyCollection<Guid> SucceededIds,
        IReadOnlyCollection<Guid> FailedIds);

    [LoggerMessage(
        EventId = 6402,
        Level = LogLevel.Warning,
        Message = "TMDB changes sync gave up on {Count} {MediaType} titles; see tmdb_changes_failed_targets.")]
    private static partial void LogExhaustedTargetsMessage(ILogger logger, string mediaType, int count);

    private static void LogExhaustedTargets(ILogger logger, string mediaTypeLabel, int count) =>
        LogExhaustedTargetsMessage(logger, mediaTypeLabel, count);

    [LoggerMessage(
        EventId = 6401,
        Level = LogLevel.Information,
        Message = "TMDB changes sync memory: mediaType={MediaType} window={StartDate}..{EndDate} stage={Stage} changedIds={ChangedIds} targetIds={TargetIds} managedHeapMb={ManagedHeapMb:F1} workingSetMb={WorkingSetMb:F1}")]
    private static partial void LogMemorySnapshotMessage(
        ILogger logger,
        string mediaType,
        DateOnly startDate,
        DateOnly endDate,
        string stage,
        int changedIds,
        int targetIds,
        double managedHeapMb,
        double workingSetMb);

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

        LogMemorySnapshotMessage(
            logger,
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
