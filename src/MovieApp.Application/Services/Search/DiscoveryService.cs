using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class DiscoveryService(
    ISearchRepository searchRepository,
    ICacheService cacheService) : IDiscoveryService
{
    private static readonly TimeSpan PopularCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TrendingCacheTtl = TimeSpan.FromMinutes(5);

    public async Task<PaginatedResult<SearchItem>> GetPopularAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);

        var cacheKey = DiscoveryPopularCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var result = await searchRepository.GetPopularAsync(criteria, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            PopularCacheTtl,
            cancellationToken);

        return result;
    }

    public async Task<PaginatedResult<SearchItem>> GetTrendingAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);

        var cacheKey = DiscoveryTrendingCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var result = await searchRepository.GetTrendingAsync(criteria, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            TrendingCacheTtl,
            cancellationToken);

        return result;
    }

    public async Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);

        var cacheKey = DiscoveryNewReleasesCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var result = await searchRepository.GetNewReleasesAsync(criteria, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            PopularCacheTtl,
            cancellationToken);

        return result;
    }

    public async Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);

        var cacheKey = DiscoveryTopRatedCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var result = await searchRepository.GetTopRatedAsync(criteria, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            PopularCacheTtl,
            cancellationToken);

        return result;
    }

    public async Task<PaginatedResult<SearchItem>> GetByGenreAsync(
        string genreName,
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscoveryCriteria(criteria);

        var cacheKey = DiscoveryGenreCacheKeys.Create(genreName, criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var result = await searchRepository.GetByGenreAsync(genreName, criteria, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            PopularCacheTtl,
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
