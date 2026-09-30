using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Keywords;

public sealed class MdbListKeywordBackfillService(
    IMdbListKeywordBackfillRepository backfillRepository,
    IMdbListKeywordTransportProvider transportProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<MdbListKeywordBackfillOptions> options,
    ILogger<MdbListKeywordBackfillService> logger) : IMdbListKeywordBackfillService
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

        var movieCandidates = candidates.Where(candidate => candidate.ContentType == "movie").ToList();
        var tvCandidates = candidates.Where(candidate => candidate.ContentType == "tv").ToList();

        var movieBatch = await ProcessMediaTypeBatchAsync(
            movieCandidates,
            CatalogContentType.Movie,
            concurrencyLimiter,
            itemPacing,
            itemDelayMs,
            cancellationToken);
        succeeded += movieBatch.Succeeded;
        failed += movieBatch.Failed;
        skipped += movieBatch.Skipped;
        moviesProcessed += movieBatch.Processed;

        var tvBatch = await ProcessMediaTypeBatchAsync(
            tvCandidates,
            CatalogContentType.Tv,
            concurrencyLimiter,
            itemPacing,
            itemDelayMs,
            cancellationToken);
        succeeded += tvBatch.Succeeded;
        failed += tvBatch.Failed;
        skipped += tvBatch.Skipped;
        tvShowsProcessed += tvBatch.Processed;

        return new CatalogKeywordBackfillBatchResult(
            candidates.Count,
            succeeded,
            failed,
            skipped,
            moviesProcessed,
            tvShowsProcessed);
    }

    private async Task<MediaTypeBatchCounters> ProcessMediaTypeBatchAsync(
        IReadOnlyList<CatalogKeywordBackfillCandidate> candidates,
        CatalogContentType mediaType,
        SemaphoreSlim concurrencyLimiter,
        MdbListKeywordBackfillItemPacingCoordinator? itemPacing,
        int itemDelayMs,
        CancellationToken cancellationToken)
    {
        var counters = new MediaTypeBatchCounters();
        foreach (var chunk in MdbListKeywordBatchChunks.Chunk(candidates, MdbListKeywordBatchChunks.MaxTmdbIdsPerRequest))
        {
            var tmdbIds = chunk.Select(candidate => candidate.TmdbId).ToList();
            var batch = await transportProvider.FetchKeywordsBatchAsync(mediaType, tmdbIds, cancellationToken);
            if (batch is null)
            {
                counters.Processed += chunk.Count;
                counters.Failed += chunk.Count;

                MdbListKeywordBackfillLogMessages.LogBatchTransportUnavailable(
                    logger,
                    mediaType,
                    chunk.Count);
                continue;
            }

            MdbListKeywordBackfillLogMessages.LogBatchTransportCompleted(
                logger,
                mediaType,
                chunk.Count,
                batch.KeywordsByTmdbId.Count,
                batch.Telemetry.StatusCode,
                batch.Telemetry.LatencyMilliseconds,
                batch.Telemetry.RateLimitRemaining);

            var ingestTasks = chunk.Select(async candidate =>
            {
                await concurrencyLimiter.WaitAsync(cancellationToken);

                try
                {
                    if (itemPacing is not null)
                    {
                        await itemPacing.WaitForNextStartSlotAsync(itemDelayMs, cancellationToken);
                    }

                    Interlocked.Increment(ref counters.Processed);

                    await using var scope = scopeFactory.CreateAsyncScope();
                    var ingestion = scope.ServiceProvider.GetRequiredService<IMdbListKeywordIngestionService>();
                    var keywords = batch.KeywordsByTmdbId.GetValueOrDefault(candidate.TmdbId) ?? [];
                    var result = mediaType == CatalogContentType.Movie
                        ? await ingestion.IngestMovieWithProviderKeywordsAsync(
                            candidate.CatalogId,
                            keywords,
                            cancellationToken)
                        : await ingestion.IngestTvShowWithProviderKeywordsAsync(
                            candidate.CatalogId,
                            keywords,
                            cancellationToken);

                    UpdateCounters(counters, MapOutcome(result.Status));
                }
                finally
                {
                    concurrencyLimiter.Release();
                }
            });

            await Task.WhenAll(ingestTasks);
        }

        return counters;
    }

    private sealed class MediaTypeBatchCounters
    {
        public int Succeeded;
        public int Failed;
        public int Skipped;
        public int Processed;
    }

    private static CatalogKeywordBackfillItemOutcome MapOutcome(MdbListKeywordIngestionStatus status) =>
        status switch
        {
            MdbListKeywordIngestionStatus.Succeeded => CatalogKeywordBackfillItemOutcome.Succeeded,
            MdbListKeywordIngestionStatus.NotEligible => CatalogKeywordBackfillItemOutcome.Skipped,
            MdbListKeywordIngestionStatus.CatalogNotFound => CatalogKeywordBackfillItemOutcome.Skipped,
            _ => CatalogKeywordBackfillItemOutcome.Failed,
        };

    private static void UpdateCounters(MediaTypeBatchCounters counters, CatalogKeywordBackfillItemOutcome outcome)
    {
        switch (outcome)
        {
            case CatalogKeywordBackfillItemOutcome.Succeeded:
                Interlocked.Increment(ref counters.Succeeded);
                break;
            case CatalogKeywordBackfillItemOutcome.Failed:
                Interlocked.Increment(ref counters.Failed);
                break;
            case CatalogKeywordBackfillItemOutcome.Skipped:
                Interlocked.Increment(ref counters.Skipped);
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

internal static partial class MdbListKeywordBackfillLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "MDBList keyword backfill batch transport unavailable for {MediaType} count={Count}.")]
    public static partial void LogBatchTransportUnavailable(
        ILogger logger,
        CatalogContentType mediaType,
        int count);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "MDBList keyword backfill batch transport completed for {MediaType} requested={RequestedCount} returned={ReturnedCount} status={StatusCode} duration_ms={DurationMs} rate_limit_remaining={RateLimitRemaining}.")]
    public static partial void LogBatchTransportCompleted(
        ILogger logger,
        CatalogContentType mediaType,
        int requestedCount,
        int returnedCount,
        int statusCode,
        long durationMs,
        int? rateLimitRemaining);
}
