using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Catalog;

public sealed class CatalogGenreBackfillService(
    ICatalogGenreBackfillRepository backfillRepository,
    IServiceScopeFactory scopeFactory,
    IOptions<CatalogGenreBackfillOptions> options) : ICatalogGenreBackfillService
{
    public bool IsEnabled => options.Value.Enabled;

    public async Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || batchSize <= 0)
        {
            return [];
        }

        var effectiveBatchSize = Math.Clamp(batchSize, 1, options.Value.BatchSize);
        var movieTarget = effectiveBatchSize / 2;
        var tvTarget = effectiveBatchSize - movieTarget;
        var selected = new List<CatalogGenreBackfillCandidate>(effectiveBatchSize);

        var movies = await backfillRepository.SelectMovieCandidatesAsync(movieTarget, excludeIds, cancellationToken);
        selected.AddRange(movies);

        var tvShows = await backfillRepository.SelectTvShowCandidatesAsync(tvTarget, excludeIds, cancellationToken);
        selected.AddRange(tvShows);

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

    public async Task<CatalogGenreBackfillBatchResult> ProcessBatchAsync(
        IReadOnlyList<CatalogGenreBackfillCandidate> candidates,
        ISet<Guid> runExcludeIds,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || candidates.Count == 0)
        {
            return new CatalogGenreBackfillBatchResult(0, 0, 0, 0, 0, 0, 0);
        }

        var maxConcurrency = Math.Max(1, options.Value.MaxConcurrency);
        using var concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var succeeded = 0;
        var unrepairable = 0;
        var failed = 0;
        var skipped = 0;
        var moviesProcessed = 0;
        var tvShowsProcessed = 0;
        var itemDelayMs = Math.Max(0, options.Value.DelayBetweenItemsMs);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (runExcludeIds.Contains(candidate.CatalogId))
            {
                Interlocked.Increment(ref skipped);
                continue;
            }

            await concurrencyLimiter.WaitAsync(cancellationToken);

            CatalogGenreBackfillItemOutcome outcome;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ICatalogGenreBackfillItemProcessor>();

                if (candidate.ContentType == "movie")
                {
                    Interlocked.Increment(ref moviesProcessed);
                }
                else
                {
                    Interlocked.Increment(ref tvShowsProcessed);
                }

                outcome = await processor.ProcessAsync(candidate, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                concurrencyLimiter.Release();
                throw;
            }
            catch
            {
                outcome = CatalogGenreBackfillItemOutcome.Failed;
                await RecordTransientFailureAsync(candidate, cancellationToken);
            }
            finally
            {
                concurrencyLimiter.Release();
            }

            UpdateCounters(ref succeeded, ref unrepairable, ref failed, ref skipped, outcome);

            if (outcome is CatalogGenreBackfillItemOutcome.Failed
                or CatalogGenreBackfillItemOutcome.Unrepairable
                or CatalogGenreBackfillItemOutcome.SkippedUnavailable)
            {
                runExcludeIds.Add(candidate.CatalogId);
            }

            if (itemDelayMs > 0)
            {
                await Task.Delay(itemDelayMs, cancellationToken);
            }
        }

        return new CatalogGenreBackfillBatchResult(
            candidates.Count,
            succeeded,
            unrepairable,
            failed,
            skipped,
            moviesProcessed,
            tvShowsProcessed);
    }

    public Task<CatalogGenreBackfillCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default) =>
        backfillRepository.GetCoverageAsync(cancellationToken);

    public async Task<CatalogGenreBackfillRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            var coverage = await GetCoverageAsync(cancellationToken);
            return new CatalogGenreBackfillRunResult(0, 0, 0, 0, 0, 0, 0, 0, coverage.OverallRemaining);
        }

        var runExcludeIds = new HashSet<Guid>();
        var batchesProcessed = 0;
        var selected = 0;
        var succeeded = 0;
        var unrepairable = 0;
        var failed = 0;
        var skipped = 0;
        var moviesProcessed = 0;
        var tvShowsProcessed = 0;
        var maxBatches = Math.Max(1, options.Value.MaxBatchesPerRun);
        var batchDelayMs = Math.Max(0, options.Value.DelayBetweenBatchesMs);

        while (batchesProcessed < maxBatches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = await SelectCandidatesAsync(options.Value.BatchSize, runExcludeIds, cancellationToken);
            if (candidates.Count == 0)
            {
                break;
            }

            var batchResult = await ProcessBatchAsync(candidates, runExcludeIds, cancellationToken);
            batchesProcessed++;
            selected += batchResult.Selected;
            succeeded += batchResult.Succeeded;
            unrepairable += batchResult.Unrepairable;
            failed += batchResult.Failed;
            skipped += batchResult.Skipped;
            moviesProcessed += batchResult.MoviesProcessed;
            tvShowsProcessed += batchResult.TvShowsProcessed;

            if (batchesProcessed >= maxBatches || batchDelayMs <= 0)
            {
                continue;
            }

            var nextCandidates = await SelectCandidatesAsync(1, runExcludeIds, cancellationToken);
            if (nextCandidates.Count == 0)
            {
                break;
            }

            await Task.Delay(batchDelayMs, cancellationToken);
        }

        var coverageAfter = await GetCoverageAsync(cancellationToken);
        return new CatalogGenreBackfillRunResult(
            batchesProcessed,
            selected,
            succeeded,
            unrepairable,
            failed,
            skipped,
            moviesProcessed,
            tvShowsProcessed,
            coverageAfter.OverallRemaining);
    }

    private static void UpdateCounters(
        ref int succeeded,
        ref int unrepairable,
        ref int failed,
        ref int skipped,
        CatalogGenreBackfillItemOutcome outcome)
    {
        switch (outcome)
        {
            case CatalogGenreBackfillItemOutcome.Succeeded:
                Interlocked.Increment(ref succeeded);
                break;
            case CatalogGenreBackfillItemOutcome.Unrepairable:
                Interlocked.Increment(ref unrepairable);
                break;
            case CatalogGenreBackfillItemOutcome.Failed:
                Interlocked.Increment(ref failed);
                break;
            case CatalogGenreBackfillItemOutcome.Skipped:
            case CatalogGenreBackfillItemOutcome.SkippedUnavailable:
                Interlocked.Increment(ref skipped);
                break;
        }
    }

    private async Task RecordTransientFailureAsync(
        CatalogGenreBackfillCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!TryMapContentType(candidate.ContentType, out var contentType))
        {
            return;
        }

        var attemptedAtUtc = DateTime.UtcNow;
        var nextEligibleAtUtc = CatalogGenreBackfillRetryPolicy.CalculateNextEligibleAtUtc(
            CatalogGenreRepairAttemptOutcome.FailedTransient,
            attemptedAtUtc,
            options.Value);

        await backfillRepository.UpsertRepairAttemptAsync(
            contentType,
            candidate.CatalogId,
            CatalogGenreRepairAttemptOutcome.FailedTransient,
            attemptedAtUtc,
            nextEligibleAtUtc,
            cancellationToken);
    }

    private static bool TryMapContentType(string contentType, out CatalogContentType mappedContentType)
    {
        if (contentType == "movie")
        {
            mappedContentType = CatalogContentType.Movie;
            return true;
        }

        if (contentType == "tv")
        {
            mappedContentType = CatalogContentType.Tv;
            return true;
        }

        mappedContentType = default;
        return false;
    }
}
