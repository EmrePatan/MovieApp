using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public sealed class MdbListKeywordBackfillService(
    IMdbListKeywordBackfillRepository backfillRepository,
    IServiceScopeFactory scopeFactory,
    IOptions<MdbListKeywordBackfillOptions> options) : IMdbListKeywordBackfillService
{
    public async Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default)
    {
        var effectiveBatchSize = Math.Clamp(batchSize, 1, options.Value.BatchSize);
        if (batchSize <= 0)
        {
            return [];
        }

        var movieTarget = effectiveBatchSize / 2;
        var tvTarget = effectiveBatchSize - movieTarget;
        var selected = new List<CatalogKeywordBackfillCandidate>(effectiveBatchSize);

        var movies = await backfillRepository.SelectMovieCandidatesAsync(movieTarget, excludeIds, cancellationToken);
        selected.AddRange(movies);

        var tvs = await backfillRepository.SelectTvShowCandidatesAsync(tvTarget, excludeIds, cancellationToken);
        selected.AddRange(tvs);

        var remaining = effectiveBatchSize - selected.Count;
        if (remaining <= 0)
        {
            return selected;
        }

        var selectedIds = selected.Select(candidate => candidate.CatalogId).ToHashSet();
        var combinedExclude = excludeIds.Concat(selectedIds).ToHashSet();

        if (movies.Count < movieTarget)
        {
            var additionalTvShows = await backfillRepository.SelectTvShowCandidatesAsync(
                remaining,
                combinedExclude,
                cancellationToken);
            selected.AddRange(additionalTvShows);
        }
        else
        {
            var additionalMovies = await backfillRepository.SelectMovieCandidatesAsync(
                remaining,
                combinedExclude,
                cancellationToken);
            selected.AddRange(additionalMovies);
        }

        return selected;
    }

    public async Task<CatalogKeywordBackfillBatchResult> ProcessBatchAsync(
        IReadOnlyList<CatalogKeywordBackfillCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return new CatalogKeywordBackfillBatchResult(0, 0, 0, 0, 0, 0);
        }

        var maxConcurrency = Math.Max(1, options.Value.MaxConcurrency);
        var itemDelayMs = Math.Max(0, options.Value.DelayBetweenItemsMs);
        using var concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var itemPacing = itemDelayMs > 0 ? new MdbListKeywordBackfillItemPacingCoordinator() : null;
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        var moviesProcessed = 0;
        var tvShowsProcessed = 0;

        var processingTasks = candidates.Select(async candidate =>
        {
            await concurrencyLimiter.WaitAsync(cancellationToken);

            try
            {
                if (itemPacing is not null)
                {
                    await itemPacing.WaitForNextStartSlotAsync(itemDelayMs, cancellationToken);
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IMdbListKeywordBackfillItemProcessor>();

                if (candidate.ContentType == "movie")
                {
                    Interlocked.Increment(ref moviesProcessed);
                }
                else
                {
                    Interlocked.Increment(ref tvShowsProcessed);
                }

                var outcome = await processor.ProcessAsync(candidate, cancellationToken);
                UpdateCounters(ref succeeded, ref failed, ref skipped, outcome);
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        });

        await Task.WhenAll(processingTasks);

        return new CatalogKeywordBackfillBatchResult(
            candidates.Count,
            succeeded,
            failed,
            skipped,
            moviesProcessed,
            tvShowsProcessed);
    }

    private static void UpdateCounters(
        ref int succeeded,
        ref int failed,
        ref int skipped,
        CatalogKeywordBackfillItemOutcome outcome)
    {
        switch (outcome)
        {
            case CatalogKeywordBackfillItemOutcome.Succeeded:
                Interlocked.Increment(ref succeeded);
                break;
            case CatalogKeywordBackfillItemOutcome.Failed:
                Interlocked.Increment(ref failed);
                break;
            case CatalogKeywordBackfillItemOutcome.Skipped:
                Interlocked.Increment(ref skipped);
                break;
        }
    }

    private sealed class MdbListKeywordBackfillItemPacingCoordinator
    {
        private readonly object _sync = new();
        private DateTime _nextAllowedStartUtc = DateTime.MinValue;

        public async Task WaitForNextStartSlotAsync(int delayMs, CancellationToken cancellationToken)
        {
            TimeSpan wait;
            lock (_sync)
            {
                var now = DateTime.UtcNow;
                wait = _nextAllowedStartUtc - now;
                if (wait < TimeSpan.Zero)
                {
                    wait = TimeSpan.Zero;
                }

                _nextAllowedStartUtc = (wait > TimeSpan.Zero ? _nextAllowedStartUtc : now).AddMilliseconds(delayMs);
            }

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }
        }
    }
}
