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
    ISearchRefreshLockService refreshLockService,
    DiscoveryCacheLoadCoordinator loadCoordinator,
    ILogger<DiscoveryService> logger) : IDiscoveryService
{
    private static readonly TimeSpan PopularCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TrendingCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshLockDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CachePollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly int MaxCachePollAttempts = 50;

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
        var totalStopwatch = Stopwatch.StartNew();
        var cacheLookupStopwatch = Stopwatch.StartNew();
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        cacheLookupStopwatch.Stop();
        var initialCacheLookupMs = cacheLookupStopwatch.ElapsedMilliseconds;

        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var inFlight = loadCoordinator.TryGetInFlight(cacheKey);
        if (inFlight is not null)
        {
            var result = await inFlight;
            LogDiscoveryCachePerf(
                operation,
                "WaiterInProcess",
                initialCacheLookupMs,
                lockAcquireMs: 0,
                waitForOwnerMs: totalStopwatch.ElapsedMilliseconds,
                pollCount: 0,
                postWaitCacheLookupMs: 0,
                fallbackLoadMs: 0,
                totalStopwatch.ElapsedMilliseconds);
            return result;
        }

        var lockAcquireStopwatch = Stopwatch.StartNew();
        var lockKey = DiscoveryCacheLockKeys.Create(cacheKey);
        var lockHandle = await refreshLockService.TryAcquireAsync(
            lockKey,
            RefreshLockDuration,
            cancellationToken);
        lockAcquireStopwatch.Stop();
        var lockAcquireMs = lockAcquireStopwatch.ElapsedMilliseconds;

        long waitForOwnerMs = 0;
        var pollCount = 0;
        long postWaitCacheLookupMs = 0;
        string role;

        if (lockHandle is null)
        {
            inFlight = loadCoordinator.TryGetInFlight(cacheKey);
            if (inFlight is not null)
            {
                role = "WaiterInProcess";
                var waitStopwatch = Stopwatch.StartNew();
                var result = await inFlight;
                waitStopwatch.Stop();
                waitForOwnerMs = waitStopwatch.ElapsedMilliseconds;
                totalStopwatch.Stop();
                LogDiscoveryCachePerf(
                    operation,
                    role,
                    initialCacheLookupMs,
                    lockAcquireMs,
                    waitForOwnerMs,
                    pollCount,
                    postWaitCacheLookupMs,
                    fallbackLoadMs: 0,
                    totalStopwatch.ElapsedMilliseconds);
                return result;
            }

            var redisWaitStopwatch = Stopwatch.StartNew();
            (var waited, pollCount) = await WaitForCachedDiscoveryAsync(cacheKey, cancellationToken);
            redisWaitStopwatch.Stop();
            waitForOwnerMs = redisWaitStopwatch.ElapsedMilliseconds;

            if (waited is not null)
            {
                role = "WaiterRedis";
                totalStopwatch.Stop();
                LogDiscoveryCachePerf(
                    operation,
                    role,
                    initialCacheLookupMs,
                    lockAcquireMs,
                    waitForOwnerMs,
                    pollCount,
                    postWaitCacheLookupMs,
                    fallbackLoadMs: 0,
                    totalStopwatch.ElapsedMilliseconds);
                return waited.Result;
            }
        }

        try
        {
            var postWaitLookupStopwatch = Stopwatch.StartNew();
            cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
            postWaitLookupStopwatch.Stop();
            postWaitCacheLookupMs = postWaitLookupStopwatch.ElapsedMilliseconds;

            if (cachedEntry is not null)
            {
                role = lockHandle is not null ? "OwnerRecheckHit" : "WaiterRecheckHit";
                totalStopwatch.Stop();
                LogDiscoveryCachePerf(
                    operation,
                    role,
                    initialCacheLookupMs,
                    lockAcquireMs,
                    waitForOwnerMs,
                    pollCount,
                    postWaitCacheLookupMs,
                    fallbackLoadMs: 0,
                    totalStopwatch.ElapsedMilliseconds);
                return cachedEntry.Result;
            }

            role = lockHandle is not null ? "Owner" : "FallbackLoad";
            var fallbackStopwatch = Stopwatch.StartNew();

            async Task<PaginatedResult<SearchItem>> LoadOwnerAsync() =>
                await LoadAndCacheDiscoveryAsync(
                    operation,
                    cacheKey,
                    loadCanonical,
                    ttl,
                    contentLocale,
                    role,
                    initialCacheLookupMs,
                    cancellationToken);

            var loaded = await loadCoordinator.RunInFlightAsync(cacheKey, LoadOwnerAsync);
            fallbackStopwatch.Stop();
            totalStopwatch.Stop();
            LogDiscoveryCachePerf(
                operation,
                role,
                initialCacheLookupMs,
                lockAcquireMs,
                waitForOwnerMs,
                pollCount,
                postWaitCacheLookupMs,
                fallbackLoadMs: fallbackStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds);
            return loaded;
        }
        finally
        {
            if (lockHandle is not null)
            {
                await refreshLockService.ReleaseAsync(
                    lockHandle.LockKey,
                    lockHandle.LockToken,
                    lockHandle.Backend,
                    cancellationToken);
            }
        }
    }

    private void LogDiscoveryCachePerf(
        string operation,
        string role,
        long initialCacheLookupMs,
        long lockAcquireMs,
        long waitForOwnerMs,
        int pollCount,
        long postWaitCacheLookupMs,
        long fallbackLoadMs,
        long totalMs)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        DiscoveryServiceLogMessages.LogDiscoveryCachePerf(
            logger,
            operation,
            role,
            initialCacheLookupMs,
            lockAcquireMs,
            waitForOwnerMs,
            pollCount,
            postWaitCacheLookupMs,
            fallbackLoadMs,
            totalMs);
    }

    private async Task<PaginatedResult<SearchItem>> LoadAndCacheDiscoveryAsync(
        string operation,
        string cacheKey,
        Func<Task<PaginatedResult<SearchItem>>> loadCanonical,
        TimeSpan ttl,
        string contentLocale,
        string stampedeRole,
        long initialCacheLookupMs,
        CancellationToken cancellationToken)
    {
        var totalLoadStopwatch = Stopwatch.StartNew();
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
            LogDiscoveryCacheLoadCompleted(
                logger,
                operation,
                cacheKey,
                stampedeRole,
                initialCacheLookupMs,
                canonicalLoadMs,
                overlayMs,
                cacheWriteMs,
                totalLoadStopwatch.ElapsedMilliseconds,
                result.Items.Count);

            return result;
        }
        catch (Exception)
        {
            totalLoadStopwatch.Stop();
            var failurePhase = canonicalLoadMs == 0
                ? "Canonical"
                : overlayMs == 0 && cacheWriteMs == 0
                    ? "Overlay"
                    : "CacheWrite";

            LogDiscoveryCacheLoadFailed(
                logger,
                operation,
                cacheKey,
                initialCacheLookupMs,
                failurePhase,
                totalLoadStopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private async Task<(DiscoveryCacheEntry? Entry, int PollCount)> WaitForCachedDiscoveryAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var pollCount = 0;

        for (var attempt = 0; attempt < MaxCachePollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inFlight = loadCoordinator.TryGetInFlight(cacheKey);
            if (inFlight is not null)
            {
                await inFlight.ConfigureAwait(false);
                var cachedAfterInFlight = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
                if (cachedAfterInFlight is not null)
                {
                    return (cachedAfterInFlight, pollCount);
                }
            }

            await Task.Delay(CachePollInterval, cancellationToken);
            pollCount++;

            var cached = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return (cached, pollCount);
            }
        }

        return (null, pollCount);
    }

    private static void LogDiscoveryCacheLoadCompleted(
        ILogger logger,
        string operation,
        string cacheKey,
        string stampedeRole,
        long cacheLookupMs,
        long canonicalLoadMs,
        long overlayMs,
        long cacheWriteMs,
        long totalLoadMs,
        int itemCount)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        DiscoveryServiceLogMessages.LogCacheLoadCompleted(
            logger,
            operation,
            cacheKey,
            stampedeRole,
            cacheLookupMs,
            canonicalLoadMs,
            overlayMs,
            cacheWriteMs,
            totalLoadMs,
            itemCount);
    }

    private static void LogDiscoveryCacheLoadFailed(
        ILogger logger,
        string operation,
        string cacheKey,
        long cacheLookupMs,
        string failurePhase,
        long elapsedMs)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        DiscoveryServiceLogMessages.LogCacheLoadFailed(
            logger,
            operation,
            cacheKey,
            cacheLookupMs,
            failurePhase,
            elapsedMs);
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
