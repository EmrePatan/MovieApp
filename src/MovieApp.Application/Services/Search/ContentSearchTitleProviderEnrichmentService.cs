using System.Globalization;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Search;

public sealed class ContentSearchTitleProviderEnrichmentService(
    IContentSearchTitleProviderEnrichmentRepository repository,
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowRepository tvShowRepository,
    ITvShowExternalIdResolver tvShowExternalIdResolver,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ILogger<ContentSearchTitleProviderEnrichmentService> logger) : IContentSearchTitleProviderEnrichmentService
{
    public async Task<ContentSearchTitleProviderEnrichmentResult> EnrichFromProviderAsync(
        ContentSearchTitleProviderEnrichmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Clamp(request.BatchSize, 1, 500);
        var maxItems = Math.Max(1, request.MaxItems);
        var delayMs = Math.Max(0, request.DelayBetweenRequestsMs);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        var providerCalls = 0;
        Guid? lastMovieId = request.StartAfterMovieId;
        Guid? lastTvId = request.StartAfterTvShowId;

        var enrichMovies = request.ContentType is null or CatalogContentType.Movie;
        var enrichTv = request.ContentType is null or CatalogContentType.Tv;

        if (request.OnlyMovieId is not null)
        {
            enrichTv = false;
            enrichMovies = true;
        }

        if (request.OnlyTvShowId is not null)
        {
            enrichMovies = false;
            enrichTv = true;
        }

        while (processed < maxItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = maxItems - processed;
            var take = Math.Min(batchSize, remaining);
            IReadOnlyList<ContentSearchTitleEnrichmentCandidate> batch = [];

            if (enrichMovies && processed < maxItems)
            {
                batch = await repository.SelectMovieCandidatesAsync(
                    lastMovieId,
                    request.OnlyMovieId,
                    take,
                    cancellationToken);

                if (batch.Count == 0 && !enrichTv)
                {
                    break;
                }

                if (batch.Count > 0)
                {
                    foreach (var candidate in batch)
                    {
                        if (processed >= maxItems)
                        {
                            break;
                        }

                        var outcome = await ProcessMovieAsync(candidate, cancellationToken);
                        processed++;
                        lastMovieId = candidate.ContentId;
                        UpdateCounters(ref succeeded, ref failed, ref skipped, ref providerCalls, outcome);
                        await DelayBetweenRequestsAsync(delayMs, cancellationToken);
                    }

                    if (request.OnlyMovieId is not null)
                    {
                        break;
                    }

                    continue;
                }
            }

            if (!enrichTv || processed >= maxItems)
            {
                break;
            }

            batch = await repository.SelectTvShowCandidatesAsync(
                lastTvId,
                request.OnlyTvShowId,
                Math.Min(batchSize, maxItems - processed),
                cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var candidate in batch)
            {
                if (processed >= maxItems)
                {
                    break;
                }

                var outcome = await ProcessTvShowAsync(candidate, cancellationToken);
                processed++;
                lastTvId = candidate.ContentId;
                UpdateCounters(ref succeeded, ref failed, ref skipped, ref providerCalls, outcome);
                await DelayBetweenRequestsAsync(delayMs, cancellationToken);
            }

            if (request.OnlyTvShowId is not null)
            {
                break;
            }
        }

        return new ContentSearchTitleProviderEnrichmentResult(
            processed,
            succeeded,
            failed,
            skipped,
            providerCalls,
            lastMovieId,
            lastTvId);
    }

    private async Task<EnrichmentItemOutcome> ProcessMovieAsync(
        ContentSearchTitleEnrichmentCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!candidate.TmdbId.HasValue)
        {
            return EnrichmentItemOutcome.Skipped;
        }

        try
        {
            var providerDetails = await movieDataProvider.GetMovieAsync(
                candidate.TmdbId.Value.ToString(CultureInfo.InvariantCulture),
                includeKeywords: false,
                cancellationToken);

            if (providerDetails is null)
            {
                ContentSearchTitleProviderEnrichmentLogMessages.LogMovieSkippedUnavailable(
                    logger,
                    candidate.ContentId,
                    candidate.TmdbId.Value);
                return EnrichmentItemOutcome.SkippedAfterCall;
            }

            await catalogProviderUpsertService.UpsertMovieFromProviderAsync(
                providerDetails,
                enrichKeywords: false,
                cancellationToken);

            return EnrichmentItemOutcome.SucceededAfterCall;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ContentSearchTitleProviderEnrichmentLogMessages.LogMovieFailed(
                logger,
                candidate.ContentId,
                candidate.TmdbId,
                exception);
            return EnrichmentItemOutcome.FailedAfterCall;
        }
    }

    private async Task<EnrichmentItemOutcome> ProcessTvShowAsync(
        ContentSearchTitleEnrichmentCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            var tvShow = await tvShowRepository.GetByIdAsync(candidate.ContentId, cancellationToken);
            if (tvShow is null)
            {
                return EnrichmentItemOutcome.Skipped;
            }

            var externalId = tvShowExternalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
            if (externalId is null)
            {
                return EnrichmentItemOutcome.Skipped;
            }

            var providerDetails = await tvShowDataProvider.GetTvShowAsync(
                externalId,
                includeKeywords: false,
                cancellationToken);

            if (providerDetails is null)
            {
                ContentSearchTitleProviderEnrichmentLogMessages.LogTvSkippedUnavailable(
                    logger,
                    candidate.ContentId,
                    tvShow.TmdbId ?? 0);
                return EnrichmentItemOutcome.SkippedAfterCall;
            }

            await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
                providerDetails,
                enrichKeywords: false,
                cancellationToken);

            return EnrichmentItemOutcome.SucceededAfterCall;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ContentSearchTitleProviderEnrichmentLogMessages.LogTvFailed(
                logger,
                candidate.ContentId,
                candidate.TmdbId,
                exception);
            return EnrichmentItemOutcome.FailedAfterCall;
        }
    }

    private static void UpdateCounters(
        ref int succeeded,
        ref int failed,
        ref int skipped,
        ref int providerCalls,
        EnrichmentItemOutcome outcome)
    {
        switch (outcome)
        {
            case EnrichmentItemOutcome.SucceededAfterCall:
                providerCalls++;
                succeeded++;
                break;
            case EnrichmentItemOutcome.SkippedAfterCall:
                providerCalls++;
                skipped++;
                break;
            case EnrichmentItemOutcome.FailedAfterCall:
                providerCalls++;
                failed++;
                break;
            case EnrichmentItemOutcome.Skipped:
                skipped++;
                break;
        }
    }

    private static Task DelayBetweenRequestsAsync(int delayMs, CancellationToken cancellationToken) =>
        delayMs > 0
            ? Task.Delay(delayMs, cancellationToken)
            : Task.CompletedTask;

    private enum EnrichmentItemOutcome
    {
        Skipped,
        SkippedAfterCall,
        SucceededAfterCall,
        FailedAfterCall,
    }
}
