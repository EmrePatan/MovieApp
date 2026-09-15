using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Home;

public sealed class HomeService(
    ICurrentUser currentUser,
    IServiceScopeFactory scopeFactory,
    ICacheService cacheService,
    IOptions<HomeOptions> options) : IHomeService
{
    private const string RecommendedForYouKey = "recommended-for-you";

    private static readonly HomeSectionType[] HomeSectionOrder =
    [
        HomeSectionType.HotThisWeek,
        HomeSectionType.RecommendedForYou,
        HomeSectionType.Trending,
        HomeSectionType.TopRated,
        HomeSectionType.NewReleases
    ];

    private readonly HomeOptions _options = options.Value;

    public async Task<HomeResult> GetHomeAsync(HomeCriteria criteria, CancellationToken cancellationToken = default)
    {
        ValidateCriteria(criteria);
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var cacheKey = HomeCacheKeys.Create(userId, criteria.Type, criteria.SectionSize);
        var cached = await cacheService.GetAsync<HomeCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var heroSize = Math.Min(_options.HeroSectionSize, criteria.SectionSize);
        var discoveryCriteria = new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize);

        var recommendationSectionsTask = RunScopedAsync(
            (services, ct) => services
                .GetRequiredService<IRecommendationService>()
                .GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false, ct),
            cancellationToken);

        var hotThisWeekTask = RunScopedAsync(
            (services, ct) => BuildHotThisWeekSectionAsync(
                services,
                criteria,
                heroSize,
                ct),
            cancellationToken);

        var trendingTask = RunScopedAsync(
            (services, ct) => HomeSectionBuilders.BuildDiscoverySectionAsync(
                HomeSectionType.Trending,
                "Trending Now",
                services.GetRequiredService<IDiscoveryService>()
                    .GetTrendingAsync(discoveryCriteria, ct),
                criteria,
                ct),
            cancellationToken);

        var topRatedTask = RunScopedAsync(
            (services, ct) => BuildTopRatedSectionAsync(services, criteria, ct),
            cancellationToken);

        var newReleasesTask = RunScopedAsync(
            (services, ct) => HomeSectionBuilders.BuildDiscoverySectionAsync(
                HomeSectionType.NewReleases,
                "New Releases",
                services.GetRequiredService<IDiscoveryService>()
                    .GetNewReleasesAsync(discoveryCriteria, ct),
                criteria,
                ct),
            cancellationToken);

        await Task.WhenAll(
            recommendationSectionsTask,
            hotThisWeekTask,
            trendingTask,
            topRatedTask,
            newReleasesTask);

        var recommendationSections = await recommendationSectionsTask;
        var isPersonalized = recommendationSections.Any(section => section.Key == RecommendedForYouKey);

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>
        {
            [HomeSectionType.HotThisWeek] = await hotThisWeekTask,
            [HomeSectionType.Trending] = await trendingTask,
            [HomeSectionType.TopRated] = await topRatedTask,
            [HomeSectionType.NewReleases] = await newReleasesTask
        };

        if (isPersonalized)
        {
            var recommendedSection = BuildRecommendedSection(
                recommendationSections,
                criteria,
                sectionsByType.GetValueOrDefault(HomeSectionType.HotThisWeek));

            if (recommendedSection is not null)
            {
                sectionsByType[HomeSectionType.RecommendedForYou] = recommendedSection;
            }
        }

        var orderedSections = BuildOrderedSections(sectionsByType, HomeSectionOrder);
        var result = new HomeResult(orderedSections, isPersonalized);

        await cacheService.SetAsync(
            cacheKey,
            new HomeCacheEntry { Result = result },
            TimeSpan.FromMinutes(_options.CacheTtlMinutes),
            cancellationToken);

        return result;
    }

    private static async Task<HomeSection> BuildTopRatedSectionAsync(
        IServiceProvider services,
        HomeCriteria criteria,
        CancellationToken cancellationToken)
    {
        var items = await services
            .GetRequiredService<IHomeTopRatedService>()
            .GetItemsAsync(criteria.Type, criteria.SectionSize, cancellationToken);

        var homeItems = HomeSectionBuilders.DeduplicateItems(
            items.Select(HomeMapper.FromSearchItem),
            criteria.SectionSize);

        return new HomeSection(
            HomeSectionType.TopRated,
            "Top Rated",
            HomeSectionBuilders.FilterByType(homeItems, criteria.Type),
            0);
    }

    private static async Task<HomeSection> BuildHotThisWeekSectionAsync(
        IServiceProvider services,
        HomeCriteria criteria,
        int heroSize,
        CancellationToken cancellationToken)
    {
        var items = await services
            .GetRequiredService<IHotThisWeekService>()
            .GetItemsAsync(criteria.Type, heroSize, cancellationToken);

        var homeItems = HomeSectionBuilders.DeduplicateItems(
            items.Select(HomeMapper.FromSearchItem),
            heroSize);

        return new HomeSection(
            HomeSectionType.HotThisWeek,
            "Hot This Week",
            HomeSectionBuilders.FilterByType(homeItems, criteria.Type),
            0);
    }

    private static HomeSection? BuildRecommendedSection(
        IReadOnlyList<RecommendationSection> recommendationSections,
        HomeCriteria criteria,
        HomeSection? hotThisWeekSection)
    {
        var recommended = recommendationSections
            .FirstOrDefault(section => section.Key == RecommendedForYouKey);

        if (recommended is null)
        {
            return null;
        }

        var items = HomeSectionBuilders.DeduplicateItems(
            recommended.Items.Select(HomeMapper.FromRecommendationItem),
            criteria.SectionSize);

        var heroIds = hotThisWeekSection?.Items
            .Select(item => item.Id)
            .ToHashSet() ?? [];

        if (heroIds.Count > 0)
        {
            var withoutHero = items.Where(item => !heroIds.Contains(item.Id)).ToList();
            if (withoutHero.Count >= criteria.SectionSize)
            {
                items = withoutHero.Take(criteria.SectionSize).ToList();
            }
        }

        var filteredItems = HomeSectionBuilders.FilterByType(items, criteria.Type);

        if (filteredItems.Count == 0)
        {
            return null;
        }

        return new HomeSection(
            HomeSectionType.RecommendedForYou,
            recommended.Title,
            filteredItems,
            0);
    }

    private async Task<T> RunScopedAsync<T>(
        Func<IServiceProvider, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await operation(scope.ServiceProvider, cancellationToken);
    }

    private static List<HomeSection> BuildOrderedSections(
        Dictionary<HomeSectionType, HomeSection> sectionsByType,
        HomeSectionType[] sectionOrder)
    {
        var orderedSections = new List<HomeSection>();
        var displayOrder = 1;

        foreach (var sectionType in sectionOrder)
        {
            if (!sectionsByType.TryGetValue(sectionType, out var section) || section.Items.Count == 0)
            {
                continue;
            }

            orderedSections.Add(section with { DisplayOrder = displayOrder++ });
        }

        return orderedSections;
    }

    private void ValidateCriteria(HomeCriteria criteria)
    {
        var validation = HomeValidator.ValidateSectionSize(
            criteria.SectionSize,
            1,
            _options.MaximumSectionSize);

        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }
    }
}
