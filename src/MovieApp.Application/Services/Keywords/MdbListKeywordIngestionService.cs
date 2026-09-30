using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Keywords;

public sealed class MdbListKeywordIngestionService(
    IMdbListKeywordTransportProvider transportProvider,
    IKeywordCatalogRepository keywordCatalogRepository,
    ILogger<MdbListKeywordIngestionService> logger) : IMdbListKeywordIngestionService
{
    public Task<MdbListKeywordIngestionResult> IngestMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken = default) =>
        IngestAsync(
            () => keywordCatalogRepository.GetMovieMdbListKeywordTargetAsync(movieId, cancellationToken),
            CatalogContentType.Movie,
            movieId,
            keywords => keywordCatalogRepository.ApplyMovieMdbListKeywordIngestionAsync(
                movieId,
                keywords,
                DateTime.UtcNow,
                cancellationToken),
            cancellationToken);

    public Task<MdbListKeywordIngestionResult> IngestTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        IngestAsync(
            () => keywordCatalogRepository.GetTvShowMdbListKeywordTargetAsync(tvShowId, cancellationToken),
            CatalogContentType.Tv,
            tvShowId,
            keywords => keywordCatalogRepository.ApplyTvShowMdbListKeywordIngestionAsync(
                tvShowId,
                keywords,
                DateTime.UtcNow,
                cancellationToken),
            cancellationToken);

    public async Task<MdbListKeywordIngestionResult> IngestMovieByTmdbIdAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        var movieId = await keywordCatalogRepository.FindMovieIdByTmdbIdAsync(tmdbId, cancellationToken);
        if (movieId is null)
        {
            return new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.CatalogNotFound);
        }

        return await IngestMovieAsync(movieId.Value, cancellationToken);
    }

    public async Task<MdbListKeywordIngestionResult> IngestTvShowByTmdbIdAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        var tvShowId = await keywordCatalogRepository.FindTvShowIdByTmdbIdAsync(tmdbId, cancellationToken);
        if (tvShowId is null)
        {
            return new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.CatalogNotFound);
        }

        return await IngestTvShowAsync(tvShowId.Value, cancellationToken);
    }

    public Task<MdbListKeywordIngestionResult> IngestMovieWithProviderKeywordsAsync(
        Guid movieId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        CancellationToken cancellationToken = default) =>
        ApplyIngestionAsync(
            () => keywordCatalogRepository.GetMovieMdbListKeywordTargetAsync(movieId, cancellationToken),
            CatalogContentType.Movie,
            movieId,
            providerKeywords,
            keywords => keywordCatalogRepository.ApplyMovieMdbListKeywordIngestionAsync(
                movieId,
                keywords,
                DateTime.UtcNow,
                cancellationToken),
            cancellationToken);

    public Task<MdbListKeywordIngestionResult> IngestTvShowWithProviderKeywordsAsync(
        Guid tvShowId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        CancellationToken cancellationToken = default) =>
        ApplyIngestionAsync(
            () => keywordCatalogRepository.GetTvShowMdbListKeywordTargetAsync(tvShowId, cancellationToken),
            CatalogContentType.Tv,
            tvShowId,
            providerKeywords,
            keywords => keywordCatalogRepository.ApplyTvShowMdbListKeywordIngestionAsync(
                tvShowId,
                keywords,
                DateTime.UtcNow,
                cancellationToken),
            cancellationToken);

    private Task<MdbListKeywordIngestionResult> IngestAsync(
        Func<Task<KeywordEnrichmentTarget?>> loadTarget,
        CatalogContentType mediaType,
        Guid catalogId,
        Func<IReadOnlyList<MdbListKeywordTransportItem>, Task<MdbListKeywordIngestionResult>> applyIngestion,
        CancellationToken cancellationToken) =>
        IngestAsync(
            loadTarget,
            mediaType,
            catalogId,
            async () =>
            {
                var target = await loadTarget();
                if (target is null)
                {
                    return null;
                }

                return await transportProvider.FetchKeywordsAsync(mediaType, target.TmdbId, cancellationToken);
            },
            transport => transport.Keywords,
            applyIngestion,
            cancellationToken);

    private Task<MdbListKeywordIngestionResult> ApplyIngestionAsync(
        Func<Task<KeywordEnrichmentTarget?>> loadTarget,
        CatalogContentType mediaType,
        Guid catalogId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        Func<IReadOnlyList<MdbListKeywordTransportItem>, Task<MdbListKeywordIngestionResult>> applyIngestion,
        CancellationToken cancellationToken) =>
        IngestAsync(
            loadTarget,
            mediaType,
            catalogId,
            () => Task.FromResult<MdbListKeywordsTransportResult?>(
                new MdbListKeywordsTransportResult(mediaType, 0, providerKeywords, IsNotFound: false)),
            transport => transport.Keywords,
            applyIngestion,
            cancellationToken);

    private async Task<MdbListKeywordIngestionResult> IngestAsync(
        Func<Task<KeywordEnrichmentTarget?>> loadTarget,
        CatalogContentType mediaType,
        Guid catalogId,
        Func<Task<MdbListKeywordsTransportResult?>> fetchTransport,
        Func<MdbListKeywordsTransportResult, IReadOnlyList<MdbListKeywordTransportItem>> selectKeywords,
        Func<IReadOnlyList<MdbListKeywordTransportItem>, Task<MdbListKeywordIngestionResult>> applyIngestion,
        CancellationToken cancellationToken)
    {
        var target = await loadTarget();
        if (target is null || target.TmdbId <= 0)
        {
            return new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.NotEligible);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var transport = await fetchTransport();
            if (transport is null)
            {
                MdbListKeywordIngestionLogMessages.LogTransportUnavailable(
                    logger,
                    mediaType,
                    catalogId,
                    target.TmdbId);
                return new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.TransportUnavailable);
            }

            var result = await applyIngestion(selectKeywords(transport));
            stopwatch.Stop();

            if (result.Status == MdbListKeywordIngestionStatus.PersistenceFailed)
            {
                MdbListKeywordIngestionLogMessages.LogPersistenceFailed(
                    logger,
                    mediaType,
                    catalogId,
                    target.TmdbId);
                return result;
            }

            if (result.Status == MdbListKeywordIngestionStatus.Succeeded && result.Stats is not null)
            {
                MdbListKeywordIngestionLogMessages.LogIngestionCompleted(
                    logger,
                    mediaType,
                    catalogId,
                    target.TmdbId,
                    result.Stats.ProviderKeywordCount,
                    result.Stats.PromotedCanonicalCount,
                    result.Stats.SkippedNoMatchCount,
                    result.Stats.SkippedAmbiguousCount,
                    stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            MdbListKeywordIngestionLogMessages.LogIngestionFailed(
                logger,
                mediaType,
                catalogId,
                target.TmdbId,
                exception);
            return new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.TransportUnavailable);
        }
    }
}

internal static partial class MdbListKeywordIngestionLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "MDBList keyword transport unavailable for {MediaType} {CatalogId} (TMDB {TmdbId}).")]
    public static partial void LogTransportUnavailable(
        ILogger logger,
        CatalogContentType mediaType,
        Guid catalogId,
        int tmdbId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "MDBList keyword ingestion completed for {MediaType} {CatalogId} (TMDB {TmdbId}) provider={ProviderKeywordCount} promoted={PromotedCount} skipped_no_match={SkippedNoMatchCount} skipped_ambiguous={SkippedAmbiguousCount} duration_ms={DurationMs}.")]
    public static partial void LogIngestionCompleted(
        ILogger logger,
        CatalogContentType mediaType,
        Guid catalogId,
        int tmdbId,
        int providerKeywordCount,
        int promotedCount,
        int skippedNoMatchCount,
        int skippedAmbiguousCount,
        long durationMs);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "MDBList keyword ingestion failed for {MediaType} {CatalogId} (TMDB {TmdbId}).")]
    public static partial void LogIngestionFailed(
        ILogger logger,
        CatalogContentType mediaType,
        Guid catalogId,
        int tmdbId,
        Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "MDBList keyword persistence failed for {MediaType} {CatalogId} (TMDB {TmdbId}).")]
    public static partial void LogPersistenceFailed(
        ILogger logger,
        CatalogContentType mediaType,
        Guid catalogId,
        int tmdbId);
}
