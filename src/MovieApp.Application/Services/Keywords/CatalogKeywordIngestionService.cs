using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public sealed class CatalogKeywordIngestionService(
    IKeywordsProvider keywordsProvider,
    IKeywordCatalogRepository keywordCatalogRepository,
    ILogger<CatalogKeywordIngestionService> logger) : ICatalogKeywordIngestionService
{
    public Task TryEnrichMovieKeywordsAsync(
        Guid movieId,
        bool refreshKeywords,
        IReadOnlyList<Models.Providers.ProviderKeywordSummary>? prefetchedKeywords = null,
        CancellationToken cancellationToken = default) =>
        TryEnrichAsync(
            () => keywordCatalogRepository.GetMovieKeywordTargetAsync(movieId, cancellationToken),
            tmdbId => keywordsProvider.GetMovieKeywordsAsync(tmdbId, cancellationToken),
            (keywords, syncedAtUtc) =>
                keywordCatalogRepository.SyncMovieKeywordsAsync(movieId, keywords, syncedAtUtc, cancellationToken),
            movieId,
            refreshKeywords,
            prefetchedKeywords,
            "movie",
            cancellationToken);

    public Task TryEnrichTvShowKeywordsAsync(
        Guid tvShowId,
        bool refreshKeywords,
        IReadOnlyList<Models.Providers.ProviderKeywordSummary>? prefetchedKeywords = null,
        CancellationToken cancellationToken = default) =>
        TryEnrichAsync(
            () => keywordCatalogRepository.GetTvShowKeywordTargetAsync(tvShowId, cancellationToken),
            tmdbId => keywordsProvider.GetTvShowKeywordsAsync(tmdbId, cancellationToken),
            (keywords, syncedAtUtc) =>
                keywordCatalogRepository.SyncTvShowKeywordsAsync(tvShowId, keywords, syncedAtUtc, cancellationToken),
            tvShowId,
            refreshKeywords,
            prefetchedKeywords,
            "tv",
            cancellationToken);

    private async Task TryEnrichAsync(
        Func<Task<KeywordEnrichmentTarget?>> loadTarget,
        Func<int, Task<IReadOnlyList<Models.Providers.ProviderKeywordSummary>>> fetchKeywords,
        Func<IReadOnlyList<Models.Providers.ProviderKeywordSummary>, DateTime, Task> syncKeywords,
        Guid catalogId,
        bool refreshKeywords,
        IReadOnlyList<Models.Providers.ProviderKeywordSummary>? prefetchedKeywords,
        string contentType,
        CancellationToken cancellationToken)
    {
        var target = await loadTarget();
        if (target is null || target.TmdbId <= 0)
        {
            return;
        }

        if (!refreshKeywords && target.KeywordsSyncedAtUtc is not null)
        {
            return;
        }

        try
        {
            var providerKeywords = prefetchedKeywords is not null
                ? prefetchedKeywords
                : await fetchKeywords(target.TmdbId);
            var normalizedKeywords = KeywordNormalization.Normalize(providerKeywords);
            await syncKeywords(normalizedKeywords, DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            CatalogKeywordIngestionLogMessages.LogKeywordEnrichmentFailed(
                logger,
                contentType,
                catalogId,
                target.TmdbId,
                exception);
        }
    }
}

internal static partial class CatalogKeywordIngestionLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Keyword enrichment failed for {ContentType} {CatalogId} (TMDB {TmdbId}).")]
    public static partial void LogKeywordEnrichmentFailed(
        ILogger logger,
        string contentType,
        Guid catalogId,
        int tmdbId,
        Exception exception);
}
