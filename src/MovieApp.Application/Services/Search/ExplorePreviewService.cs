using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class ExplorePreviewService(
    IDiscoveryService discoveryService,
    ICacheService cacheService) : IExplorePreviewService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<ExplorePreviewResult> GetPreviewAsync(
        ExplorePreviewCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateCriteria(criteria);

        var cacheKey = ExplorePreviewCacheKeys.Create(criteria.SectionSize);
        var cached = await cacheService.GetAsync<ExplorePreviewCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var discoveryCriteria = new DiscoveryCriteria(SearchContentType.All, 1, criteria.SectionSize);

        var trendingTask = discoveryService.GetTrendingAsync(
            discoveryCriteria,
            ContentLocaleResolver.EnglishUnitedStates,
            cancellationToken);
        var topRatedTask = discoveryService.GetTopRatedAsync(
            discoveryCriteria,
            ContentLocaleResolver.EnglishUnitedStates,
            cancellationToken);
        var newReleasesTask = discoveryService.GetNewReleasesAsync(
            discoveryCriteria,
            ContentLocaleResolver.EnglishUnitedStates,
            cancellationToken);

        await Task.WhenAll(trendingTask, topRatedTask, newReleasesTask);

        var result = new ExplorePreviewResult(
            await trendingTask,
            await topRatedTask,
            await newReleasesTask);

        await cacheService.SetAsync(
            cacheKey,
            new ExplorePreviewCacheEntry { Result = result },
            CacheTtl,
            cancellationToken);

        return result;
    }

    private static void ValidateCriteria(ExplorePreviewCriteria criteria)
    {
        var validation = HomeValidator.ValidateSectionSize(
            criteria.SectionSize,
            1,
            20);

        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }
    }

    private sealed class ExplorePreviewCacheEntry
    {
        public required ExplorePreviewResult Result { get; init; }
    }
}
