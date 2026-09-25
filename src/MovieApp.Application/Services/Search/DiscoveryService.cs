using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class DiscoveryService(
    ISearchRepository searchRepository,
    ICacheService cacheService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService,
    ILogger<DiscoveryService> logger) : IDiscoveryService
{
    private static readonly TimeSpan PopularCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TrendingCacheTtl = TimeSpan.FromMinutes(5);

    public Task<PaginatedResult<SearchItem>> GetPopularAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);
        return GetCachedDiscoveryAsync(
            "Popular",
            DiscoveryPopularCacheKeys.Create(criteria, contentLocale),
            () => searchRepository.GetPopularAsync(criteria, cancellationToken),
            PopularCacheTtl,
            contentLocale,
            cancellationToken);
    }

    public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);
        return GetCachedDiscoveryAsync(
            "Trending",
            DiscoveryTrendingCacheKeys.Create(criteria, contentLocale),
            () => searchRepository.GetTrendingAsync(criteria, cancellationToken),
            TrendingCacheTtl,
            contentLocale,
            cancellationToken);
    }

    public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);
        return GetCachedDiscoveryAsync(
            "NewReleases",
            DiscoveryNewReleasesCacheKeys.Create(criteria, contentLocale),
            () => searchRepository.GetNewReleasesAsync(criteria, cancellationToken),
            PopularCacheTtl,
            contentLocale,
            cancellationToken);
    }

    public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);
        return GetCachedDiscoveryAsync(
            "TopRated",
            DiscoveryTopRatedCacheKeys.Create(criteria, contentLocale),
            () => searchRepository.GetTopRatedAsync(criteria, cancellationToken),
            PopularCacheTtl,
            contentLocale,
            cancellationToken);
    }

    public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
        string genreName,
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);
        return GetCachedDiscoveryAsync(
            $"ByGenre:{genreName}",
            DiscoveryGenreCacheKeys.Create(genreName, criteria, contentLocale),
            () => searchRepository.GetByGenreAsync(genreName, criteria, cancellationToken),
            PopularCacheTtl,
            contentLocale,
            cancellationToken);
    }

    private async Task<PaginatedResult<SearchItem>> GetCachedDiscoveryAsync(
        string operation,
        string cacheKey,
        Func<Task<PaginatedResult<SearchItem>>> loadCanonical,
        TimeSpan ttl,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var totalLoadStopwatch = Stopwatch.StartNew();
        var cacheLookupStopwatch = Stopwatch.StartNew();
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        cacheLookupStopwatch.Stop();

        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        long canonicalLoadMs = 0;
        long overlayMs = 0;
        long cacheWriteMs = 0;

        try
        {
            var canonicalLoadStopwatch = Stopwatch.StartNew();
            var canonical = await loadCanonical();
            canonicalLoadStopwatch.Stop();
            canonicalLoadMs = canonicalLoadStopwatch.ElapsedMilliseconds;

            var overlayStopwatch = Stopwatch.StartNew();
            var result = await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
                canonical,
                contentLocale,
                cancellationToken);
            overlayStopwatch.Stop();
            overlayMs = overlayStopwatch.ElapsedMilliseconds;

            var cacheWriteStopwatch = Stopwatch.StartNew();
            await cacheService.SetAsync(
                cacheKey,
                new DiscoveryCacheEntry { Result = result },
                ttl,
                cancellationToken);
            cacheWriteStopwatch.Stop();
            cacheWriteMs = cacheWriteStopwatch.ElapsedMilliseconds;

            totalLoadStopwatch.Stop();
            DiscoveryServiceLogMessages.LogCacheLoadCompleted(
                logger,
                operation,
                cacheKey,
                cacheLookupStopwatch.ElapsedMilliseconds,
                canonicalLoadMs,
                overlayMs,
                cacheWriteMs,
                totalLoadStopwatch.ElapsedMilliseconds,
                result.Items.Count);

            return result;
        }
        catch (Exception exception)
        {
            totalLoadStopwatch.Stop();
            var failurePhase = canonicalLoadMs == 0
                ? "Canonical"
                : overlayMs == 0 && cacheWriteMs == 0
                    ? "Overlay"
                    : "CacheWrite";

            if (logger.IsEnabled(LogLevel.Information))
            {
                DiscoveryServiceLogMessages.LogCacheLoadFailed(
                    logger,
                    operation,
                    cacheKey,
                    cacheLookupStopwatch.ElapsedMilliseconds,
                    failurePhase,
                    totalLoadStopwatch.ElapsedMilliseconds,
                    exception.GetType().Name);
            }

            throw;
        }
    }

    private static void ValidateDiscoveryCriteria(DiscoveryCriteria criteria)
    {
        var paginationValidation = AdvancedSearchValidator.ValidatePagination(criteria.Page, criteria.PageSize);
        if (!paginationValidation.IsValid)
        {
            throw new ValidationException(paginationValidation.ErrorMessage!);
        }
    }
}
