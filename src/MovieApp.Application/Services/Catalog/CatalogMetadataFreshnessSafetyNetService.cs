using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.MovieChanges;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.Application.Services.Catalog;

public sealed class CatalogMetadataFreshnessSafetyNetService(
    ICatalogMetadataFreshnessRepository freshnessRepository,
    IMovieChangesTargetedRefreshService movieRefreshService,
    ITvShowChangesTargetedRefreshService tvRefreshService,
    IOptions<CatalogMetadataFreshnessOptions> options,
    ILogger<CatalogMetadataFreshnessSafetyNetService> logger) : ICatalogMetadataFreshnessSafetyNetService
{
    public async Task<CatalogMetadataFreshnessSafetyNetResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = options.Value;

        if (!settings.SafetyNetEnabled)
        {
            return new CatalogMetadataFreshnessSafetyNetResult(0, 0, 0, 0, 0, 0, 0, 0, stopwatch.ElapsedMilliseconds);
        }

        var staleBeforeUtc = DateTime.UtcNow.AddHours(-settings.FreshnessThresholdHours);
        var maxPerRun = Math.Max(1, settings.SafetyNetMaxItemsPerRun);
        var batchSize = Math.Clamp(settings.SafetyNetBatchSize, 1, maxPerRun);

        var eligibleMovies = await freshnessRepository.CountStaleDiscoveryRelevantMoviesAsync(
            staleBeforeUtc,
            cancellationToken);
        var eligibleTv = await freshnessRepository.CountStaleDiscoveryRelevantTvShowsAsync(
            staleBeforeUtc,
            cancellationToken);
        var eligibleTotal = eligibleMovies + eligibleTv;

        var movieTake = Math.Min(batchSize, Math.Max(0, maxPerRun / 2));
        var tvTake = Math.Min(batchSize, maxPerRun - movieTake);
        if (movieTake == 0 && tvTake > 0 && eligibleMovies > 0)
        {
            movieTake = Math.Min(batchSize, maxPerRun);
            tvTake = 0;
        }

        if (tvTake == 0 && movieTake < maxPerRun && eligibleTv > 0)
        {
            tvTake = Math.Min(batchSize, maxPerRun - movieTake);
        }

        var movieIds = await freshnessRepository.SelectStaleDiscoveryRelevantMovieIdsAsync(
            staleBeforeUtc,
            movieTake,
            cancellationToken);
        var tvIds = await freshnessRepository.SelectStaleDiscoveryRelevantTvShowIdsAsync(
            staleBeforeUtc,
            tvTake,
            cancellationToken);

        var refreshed = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var movieId in movieIds)
        {
            var outcome = await RefreshMovieAsync(movieId, cancellationToken);
            switch (outcome)
            {
                case TmdbChangesTargetRefreshOutcome.Refreshed:
                    refreshed++;
                    break;
                case TmdbChangesTargetRefreshOutcome.SkippedNotFound:
                case TmdbChangesTargetRefreshOutcome.SkippedUnavailable:
                    skipped++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        var boundaryDate = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var tvShowId in tvIds)
        {
            var outcome = await RefreshTvShowAsync(tvShowId, boundaryDate, cancellationToken);
            switch (outcome)
            {
                case TmdbChangesTargetRefreshOutcome.Refreshed:
                    refreshed++;
                    break;
                case TmdbChangesTargetRefreshOutcome.SkippedNotFound:
                case TmdbChangesTargetRefreshOutcome.SkippedUnavailable:
                    skipped++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        stopwatch.Stop();
        var selected = movieIds.Count + tvIds.Count;
        var remainingEstimate = Math.Max(0, eligibleTotal - selected);

        CatalogMetadataFreshnessLogMessages.LogSafetyNetCompleted(
            logger,
            eligibleTotal,
            selected,
            movieIds.Count,
            tvIds.Count,
            refreshed,
            skipped,
            failed,
            remainingEstimate,
            stopwatch.ElapsedMilliseconds);

        return new CatalogMetadataFreshnessSafetyNetResult(
            eligibleTotal,
            selected,
            movieIds.Count,
            tvIds.Count,
            refreshed,
            skipped,
            failed,
            remainingEstimate,
            stopwatch.ElapsedMilliseconds);
    }

    private async Task<TmdbChangesTargetRefreshOutcome> RefreshMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await movieRefreshService.RefreshRelevantMovieAsync(movieId, cancellationToken);
            return result.Outcome;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CatalogMetadataFreshnessLogMessages.LogSafetyNetItemFailed(
                logger,
                exception,
                "movie",
                movieId);
            return TmdbChangesTargetRefreshOutcome.Failed;
        }
    }

    private async Task<TmdbChangesTargetRefreshOutcome> RefreshTvShowAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await tvRefreshService.RefreshRelevantShowAsync(
                tvShowId,
                boundaryDate,
                boundaryDate,
                cancellationToken);
            return result.Outcome;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CatalogMetadataFreshnessLogMessages.LogSafetyNetItemFailed(
                logger,
                exception,
                "tv",
                tvShowId);
            return TmdbChangesTargetRefreshOutcome.Failed;
        }
    }
}
