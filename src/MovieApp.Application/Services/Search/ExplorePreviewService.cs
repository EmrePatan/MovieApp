using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class ExplorePreviewService(
    IServiceScopeFactory scopeFactory,
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

        var trendingTask = RunScopedAsync(
            (services, ct) => services.GetRequiredService<IDiscoveryService>()
                .GetTrendingAsync(discoveryCriteria, ContentLocaleResolver.EnglishUnitedStates, ct),
            cancellationToken);
        var topRatedTask = RunScopedAsync(
            (services, ct) => services.GetRequiredService<IDiscoveryService>()
                .GetTopRatedAsync(discoveryCriteria, ContentLocaleResolver.EnglishUnitedStates, ct),
            cancellationToken);
        var newReleasesTask = RunScopedAsync(
            (services, ct) => services.GetRequiredService<IDiscoveryService>()
                .GetNewReleasesAsync(discoveryCriteria, ContentLocaleResolver.EnglishUnitedStates, ct),
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

    private async Task<T> RunScopedAsync<T>(
        Func<IServiceProvider, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await operation(scope.ServiceProvider, cancellationToken);
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
