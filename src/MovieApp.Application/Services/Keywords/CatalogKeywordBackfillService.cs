using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public sealed class CatalogKeywordBackfillService(
    ICatalogKeywordBackfillRepository backfillRepository,
    ICatalogKeywordIngestionService keywordIngestionService,
    IOptions<CatalogKeywordBackfillOptions> options) : ICatalogKeywordBackfillService
{
    public async Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
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

        var movies = await backfillRepository.SelectMovieCandidatesAsync(movieTarget, [], cancellationToken);
        selected.AddRange(movies);

        var tvs = await backfillRepository.SelectTvShowCandidatesAsync(tvTarget, [], cancellationToken);
        selected.AddRange(tvs);

        var remaining = effectiveBatchSize - selected.Count;
        if (remaining <= 0)
        {
            return selected;
        }

        var selectedIds = selected.Select(candidate => candidate.CatalogId).ToHashSet();

        if (movies.Count < movieTarget)
        {
            var additionalTvShows = await backfillRepository.SelectTvShowCandidatesAsync(
                remaining,
                selectedIds,
                cancellationToken);
            selected.AddRange(additionalTvShows);
        }
        else
        {
            var additionalMovies = await backfillRepository.SelectMovieCandidatesAsync(
                remaining,
                selectedIds,
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
        using var concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
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
                if (candidate.ContentType == "movie")
                {
                    Interlocked.Increment(ref moviesProcessed);
                    var outcome = await ProcessMovieAsync(candidate.CatalogId, cancellationToken);
                    UpdateCounters(ref succeeded, ref failed, ref skipped, outcome);
                }
                else
                {
                    Interlocked.Increment(ref tvShowsProcessed);
                    var outcome = await ProcessTvShowAsync(candidate.CatalogId, cancellationToken);
                    UpdateCounters(ref succeeded, ref failed, ref skipped, outcome);
                }
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

    public Task<CatalogKeywordCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default) =>
        backfillRepository.GetCoverageAsync(cancellationToken);

    private async Task<ProcessingOutcome> ProcessMovieAsync(Guid movieId, CancellationToken cancellationToken)
    {
        if (await backfillRepository.IsMovieKeywordSyncedAsync(movieId, cancellationToken))
        {
            return ProcessingOutcome.Skipped;
        }

        await keywordIngestionService.TryEnrichMovieKeywordsAsync(
            movieId,
            refreshKeywords: false,
            cancellationToken);

        return await backfillRepository.IsMovieKeywordSyncedAsync(movieId, cancellationToken)
            ? ProcessingOutcome.Succeeded
            : ProcessingOutcome.Failed;
    }

    private async Task<ProcessingOutcome> ProcessTvShowAsync(Guid tvShowId, CancellationToken cancellationToken)
    {
        if (await backfillRepository.IsTvShowKeywordSyncedAsync(tvShowId, cancellationToken))
        {
            return ProcessingOutcome.Skipped;
        }

        await keywordIngestionService.TryEnrichTvShowKeywordsAsync(
            tvShowId,
            refreshKeywords: false,
            cancellationToken);

        return await backfillRepository.IsTvShowKeywordSyncedAsync(tvShowId, cancellationToken)
            ? ProcessingOutcome.Succeeded
            : ProcessingOutcome.Failed;
    }

    private static void UpdateCounters(
        ref int succeeded,
        ref int failed,
        ref int skipped,
        ProcessingOutcome outcome)
    {
        switch (outcome)
        {
            case ProcessingOutcome.Succeeded:
                Interlocked.Increment(ref succeeded);
                break;
            case ProcessingOutcome.Failed:
                Interlocked.Increment(ref failed);
                break;
            case ProcessingOutcome.Skipped:
                Interlocked.Increment(ref skipped);
                break;
        }
    }

    private enum ProcessingOutcome
    {
        Succeeded,
        Failed,
        Skipped
    }
}
