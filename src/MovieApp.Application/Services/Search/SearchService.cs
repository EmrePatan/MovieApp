using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Common;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class SearchService(
    ISearchRepository searchRepository,
    ISearchHistoryRepository searchHistoryRepository,
    ISearchProviderRefreshRepository refreshRepository,
    ICurrentUser currentUser,
    ICacheService cacheService,
    IUnifiedSearchProviderIngestionService providerIngestionService,
    ISearchRefreshLockService refreshLockService,
    IOptions<SearchOptions> searchOptions) : ISearchService
{
    private static readonly TimeSpan MinimumRefreshWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshWaitSafetyMargin = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RefreshPollInterval = TimeSpan.FromMilliseconds(200);

    public async Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var validation = AdvancedSearchValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var options = searchOptions.Value;
        var cacheKey = UnifiedSearchCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<UnifiedSearchCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            await TryRecordSearchHistoryAsync(criteria, cancellationToken);
            return cachedEntry.Result;
        }

        var utcNow = DateTime.UtcNow;
        var normalizedQuery = GetNormalizedQuery(criteria);
        var result = await searchRepository.SearchAsync(criteria, cancellationToken);
        var lastRefreshedAtUtc = normalizedQuery is null
            ? null
            : await refreshRepository.GetLastRefreshedAtUtcAsync(
                normalizedQuery,
                criteria.Type,
                criteria.Page,
                cancellationToken);

        if (!UnifiedSearchProviderPolicy.NeedsProviderRefresh(
                criteria,
                result,
                lastRefreshedAtUtc,
                utcNow,
                options.ProviderRefreshInterval))
        {
            await TryCacheResultAsync(cacheKey, result, criteria, providerRefreshFullySucceeded: false, providerRefreshFailedOrPartial: false, options, cancellationToken);
            await TryRecordSearchHistoryAsync(criteria, cancellationToken);
            return result;
        }

        var lockKey = SearchRefreshLockKeys.Create(criteria);
        var lockHandle = await refreshLockService.TryAcquireAsync(
            lockKey,
            options.ProviderRefreshLockDuration,
            cancellationToken);

        if (lockHandle is null)
        {
            result = await SearchRefreshCoordinator.WaitForConcurrentRefreshAsync(
                () => TryGetCachedResultAsync(cacheKey, cancellationToken),
                () => LoadCatalogStateAsync(criteria, normalizedQuery, cancellationToken),
                criteria,
                utcNow,
                options,
                cancellationToken);

            var lastRefreshedAfterWait = normalizedQuery is null
                ? null
                : await refreshRepository.GetLastRefreshedAtUtcAsync(
                    normalizedQuery,
                    criteria.Type,
                    criteria.Page,
                    cancellationToken);

            if (!UnifiedSearchProviderPolicy.NeedsProviderRefresh(
                    criteria,
                    result,
                    lastRefreshedAfterWait,
                    DateTime.UtcNow,
                    options.ProviderRefreshInterval))
            {
                await TryCacheResultAsync(
                    cacheKey,
                    result,
                    criteria,
                    providerRefreshFullySucceeded: false,
                    providerRefreshFailedOrPartial: false,
                    options,
                    cancellationToken);
            }

            await TryRecordSearchHistoryAsync(criteria, cancellationToken);
            return result;
        }

        try
        {
            result = await ExecuteProviderRefreshAsync(
                criteria,
                normalizedQuery,
                cacheKey,
                result,
                options,
                cancellationToken);
        }
        finally
        {
            await refreshLockService.ReleaseAsync(lockHandle.LockKey, lockHandle.LockToken, cancellationToken);
        }

        await TryRecordSearchHistoryAsync(criteria, cancellationToken);
        return result;
    }

    private async Task<PaginatedResult<SearchItem>> ExecuteProviderRefreshAsync(
        SearchCriteria criteria,
        string? normalizedQuery,
        string cacheKey,
        PaginatedResult<SearchItem> catalogResult,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        var catalogBeforeRefresh = catalogResult;
        UnifiedSearchProviderIngestionResult ingestionResult;

        try
        {
            ingestionResult = await providerIngestionService.IngestAsync(criteria, cancellationToken);
        }
        catch (Exception)
        {
            if (catalogBeforeRefresh.TotalCount > 0)
            {
                return catalogBeforeRefresh;
            }

            throw new InvalidOperationException("Search provider is temporarily unavailable.");
        }

        var refreshedResult = await searchRepository.SearchAsync(criteria, cancellationToken);
        var providerRefreshFullySucceeded = ingestionResult.IsFullySuccessful;
        var providerRefreshFailedOrPartial = !providerRefreshFullySucceeded &&
            (ingestionResult.MovieRefreshAttempted || ingestionResult.TvRefreshAttempted);

        if (providerRefreshFullySucceeded && normalizedQuery is not null)
        {
            await refreshRepository.SetLastRefreshedAtUtcAsync(
                normalizedQuery,
                criteria.Type,
                criteria.Page,
                DateTime.UtcNow,
                cancellationToken);
        }

        var result = refreshedResult;

        if (providerRefreshFailedOrPartial &&
            !UnifiedSearchProviderPolicy.CanSatisfyRequestedPage(refreshedResult, criteria) &&
            UnifiedSearchProviderPolicy.CanSatisfyRequestedPage(catalogBeforeRefresh, criteria))
        {
            result = catalogBeforeRefresh;
        }
        else if (!providerRefreshFullySucceeded && catalogBeforeRefresh.TotalCount > 0)
        {
            result = catalogBeforeRefresh;
        }
        else if (providerRefreshFailedOrPartial &&
                 !UnifiedSearchProviderPolicy.CanSatisfyRequestedPage(refreshedResult, criteria) &&
                 !UnifiedSearchProviderPolicy.CanSatisfyRequestedPage(catalogBeforeRefresh, criteria))
        {
            throw new InvalidOperationException("Search provider is temporarily unavailable.");
        }

        await TryCacheResultAsync(
            cacheKey,
            result,
            criteria,
            providerRefreshFullySucceeded,
            providerRefreshFailedOrPartial,
            options,
            cancellationToken);

        return result;
    }

    private async Task TryCacheResultAsync(
        string cacheKey,
        PaginatedResult<SearchItem> result,
        SearchCriteria criteria,
        bool providerRefreshFullySucceeded,
        bool providerRefreshFailedOrPartial,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        if (!UnifiedSearchProviderPolicy.ShouldCacheAfterSearch(
                result,
                criteria,
                providerRefreshFullySucceeded,
                providerRefreshFailedOrPartial))
        {
            return;
        }

        await cacheService.SetAsync(
            cacheKey,
            new UnifiedSearchCacheEntry { Result = result },
            options.CacheDuration,
            cancellationToken);
    }

    private async Task<PaginatedResult<SearchItem>?> TryGetCachedResultAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var cachedEntry = await cacheService.GetAsync<UnifiedSearchCacheEntry>(cacheKey, cancellationToken);
        return cachedEntry?.Result;
    }

    private async Task<(PaginatedResult<SearchItem> Result, DateTime? LastRefreshedAtUtc)> LoadCatalogStateAsync(
        SearchCriteria criteria,
        string? normalizedQuery,
        CancellationToken cancellationToken)
    {
        var result = await searchRepository.SearchAsync(criteria, cancellationToken);
        if (normalizedQuery is null)
        {
            return (result, null);
        }

        var lastRefreshedAtUtc = await refreshRepository.GetLastRefreshedAtUtcAsync(
            normalizedQuery,
            criteria.Type,
            criteria.Page,
            cancellationToken);

        return (result, lastRefreshedAtUtc);
    }

    private static string? GetNormalizedQuery(SearchCriteria criteria) =>
        string.IsNullOrWhiteSpace(criteria.Query)
            ? null
            : QueryNormalizer.Normalize(criteria.Query);

    private static TimeSpan ResolveRefreshWaitDuration(SearchOptions options)
    {
        var waitDuration = options.ProviderRefreshLockDuration - RefreshWaitSafetyMargin;
        return waitDuration < MinimumRefreshWait ? MinimumRefreshWait : waitDuration;
    }

    private async Task TryRecordSearchHistoryAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(criteria.Query) || !currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return;
        }

        var queryValidation = AdvancedSearchValidator.ValidateQuery(criteria.Query);
        if (!queryValidation.IsValid)
        {
            return;
        }

        var normalizedQuery = QueryNormalizer.Normalize(criteria.Query);
        var displayQuery = QueryNormalizer.CollapseWhitespace(criteria.Query);

        await searchHistoryRepository.RecordSearchAsync(
            currentUser.UserId.Value,
            displayQuery,
            normalizedQuery,
            DateTime.UtcNow,
            cancellationToken);
    }

    private sealed class SearchRefreshCoordinator
    {
        public static async Task<PaginatedResult<SearchItem>> WaitForConcurrentRefreshAsync(
            Func<Task<PaginatedResult<SearchItem>?>> tryGetCachedResultAsync,
            Func<Task<(PaginatedResult<SearchItem> Result, DateTime? LastRefreshedAtUtc)>> loadCatalogStateAsync,
            SearchCriteria criteria,
            DateTime utcNow,
            SearchOptions options,
            CancellationToken cancellationToken)
        {
            var deadline = utcNow + ResolveRefreshWaitDuration(options);
            PaginatedResult<SearchItem>? latestCatalog = null;

            while (DateTime.UtcNow <= deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var cachedResult = await tryGetCachedResultAsync();
                if (cachedResult is not null)
                {
                    return cachedResult;
                }

                var (catalogResult, lastRefreshedAtUtc) = await loadCatalogStateAsync();
                latestCatalog = catalogResult;

                if (!UnifiedSearchProviderPolicy.NeedsProviderRefresh(
                        criteria,
                        catalogResult,
                        lastRefreshedAtUtc,
                        DateTime.UtcNow,
                        options.ProviderRefreshInterval))
                {
                    return catalogResult;
                }

                await Task.Delay(RefreshPollInterval, cancellationToken);
            }

            return latestCatalog ?? (await loadCatalogStateAsync()).Result;
        }
    }
}
