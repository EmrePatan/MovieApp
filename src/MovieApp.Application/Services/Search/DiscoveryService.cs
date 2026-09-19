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
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService) : IDiscoveryService
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
            DiscoveryGenreCacheKeys.Create(genreName, criteria, contentLocale),
            () => searchRepository.GetByGenreAsync(genreName, criteria, cancellationToken),
            PopularCacheTtl,
            contentLocale,
            cancellationToken);
    }

    private async Task<PaginatedResult<SearchItem>> GetCachedDiscoveryAsync(
        string cacheKey,
        Func<Task<PaginatedResult<SearchItem>>> loadCanonical,
        TimeSpan ttl,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var canonical = await loadCanonical();
        var result = await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
            canonical,
            contentLocale,
            cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            ttl,
            cancellationToken);

        return result;
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
