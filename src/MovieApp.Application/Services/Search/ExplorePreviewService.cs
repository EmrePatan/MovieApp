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
    ICacheService cacheService,
    ExplorePreviewLoadCoordinator loadCoordinator) : IExplorePreviewService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);

    public async Task<ExplorePreviewResult> GetPreviewAsync(
        ExplorePreviewCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        ValidateCriteria(criteria);

        var cacheKey = ExplorePreviewCacheKeys.Create(criteria.SectionSize, contentLocale);
        var cached = await cacheService.GetAsync<ExplorePreviewCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        return await loadCoordinator.RunInFlightAsync(
                cacheKey,
                () => LoadPreviewAsync(criteria, contentLocale, cacheKey, cancellationToken))
            .WaitAsync(cancellationToken);
    }

    private async Task<ExplorePreviewResult> LoadPreviewAsync(
        ExplorePreviewCriteria criteria,
        string contentLocale,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var cached = await cacheService.GetAsync<ExplorePreviewCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var discoveryCriteria = new DiscoveryCriteria(SearchContentType.All, 1, criteria.SectionSize);
        var hiddenGemsCriteria = DiscoverTitleRailCriteria.Create(
            DiscoverBrowseMode.HiddenGems,
            1,
            criteria.SectionSize);
        var popularCriteria = DiscoverTitleRailCriteria.Create(
            DiscoverBrowseMode.Popular,
            1,
            criteria.SectionSize);

        var hiddenGemsTask = RunScopedAsync(
            (services, ct) => services.GetRequiredService<IDiscoverBrowseService>()
                .BrowseAsync(hiddenGemsCriteria, contentLocale, ct),
            cancellationToken);
        var popularTask = RunScopedAsync(
            (services, ct) => services.GetRequiredService<IDiscoverBrowseService>()
                .BrowseAsync(popularCriteria, contentLocale, ct),
            cancellationToken);
        var newReleasesTask = RunScopedAsync(
            (services, ct) => services.GetRequiredService<IDiscoveryService>()
                .GetCatalogListNewReleasesAsync(discoveryCriteria, contentLocale, ct),
            cancellationToken);
        var topRatedTask = RunScopedAsync(
            (services, ct) => services.GetRequiredService<IDiscoveryService>()
                .GetCatalogListTopRatedAsync(discoveryCriteria, contentLocale, ct),
            cancellationToken);

        await Task.WhenAll(hiddenGemsTask, popularTask, newReleasesTask, topRatedTask);

        var result = new ExplorePreviewResult(
            DiscoverRailCatalog.Order,
            await hiddenGemsTask,
            await popularTask,
            await newReleasesTask,
            await topRatedTask);

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
