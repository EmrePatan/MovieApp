using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Home;

public sealed class HomeService(
    ICurrentUser currentUser,
    IServiceScopeFactory scopeFactory,
    ICacheService cacheService,
    IOptions<HomeOptions> options,
    IOptions<ReleaseRegionOptions> releaseRegionOptions,
    IOptions<RecommendationOptions> recommendationOptions,
    ILogger<HomeService> logger) : IHomeService
{
    private const string RecommendedForYouKey = "recommended-for-you";

    private static readonly HomeSectionType[] HomeSectionOrder =
    [
        HomeSectionType.HotThisWeek,
        HomeSectionType.RecommendedForYou,
        HomeSectionType.ComingUp,
        HomeSectionType.Trending,
        HomeSectionType.TopRated,
        HomeSectionType.NewReleases
    ];

    private static readonly HomeSectionType[] BrowseSectionOrder =
    [
        HomeSectionType.HotThisWeek,
        HomeSectionType.Trending,
        HomeSectionType.TopRated,
        HomeSectionType.NewReleases
    ];

    private static readonly HomeSectionType[] PersonalizedSectionOrder =
    [
        HomeSectionType.RecommendedForYou,
        HomeSectionType.ComingUp
    ];

    private readonly HomeOptions _options = options.Value;
    private readonly ReleaseRegionOptions _releaseRegionOptions = releaseRegionOptions.Value;
    private readonly RecommendationOptions _recommendationOptions = recommendationOptions.Value;
    private readonly UserRecommendationCacheGeneration _recommendationCacheGeneration = new(cacheService);

    public async Task<HomeResult> GetHomeAsync(
        HomeCriteria criteria,
        string contentLocale,
        string? releaseRegion = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        ValidateCriteria(criteria);
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var recommendationGeneration = await _recommendationCacheGeneration.GetAsync(userId, cancellationToken);
        var releaseRegionKey = ResolveReleaseRegion(releaseRegion);
        var cacheKey = HomeCacheKeys.Create(
            userId,
            criteria.Type,
            criteria.SectionSize,
            contentLocale,
            releaseRegionKey,
            recommendationGeneration);
        var cacheLookupStopwatch = Stopwatch.StartNew();
        var cached = await cacheService.GetAsync<HomeCacheEntry>(cacheKey, cancellationToken);
        cacheLookupStopwatch.Stop();

        if (cached is not null)
        {
            totalStopwatch.Stop();
            HomeServiceLogMessages.LogCacheHit(
                logger,
                "HIT",
                totalStopwatch.ElapsedMilliseconds,
                cacheLookupStopwatch.ElapsedMilliseconds);
            return cached.Result;
        }

        var heroSize = Math.Min(_options.HeroSectionSize, criteria.SectionSize);
        var discoveryCriteria = new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize);
        var recommendationLimit = RecommendationCandidateLimit(criteria.SectionSize);

        var recommendationSectionsTask = RunScopedTimedAsync(
            (services, ct) => services
                .GetRequiredService<IRecommendationService>()
                .GetHomeRecommendationsForCurrentUserAsync(
                    includeColdStartDiscoverySections: false,
                    contentLocale,
                    MapRecommendationContentType(criteria.Type),
                    recommendationLimit,
                    diversify: false,
                    ct),
            cancellationToken);

        var weeklySectionsTask = RunScopedTimedAsync(
            (services, ct) => BuildWeeklyHeroAndTrendingSectionsAsync(
                services,
                criteria,
                heroSize,
                contentLocale,
                ct),
            cancellationToken);

        var comingUpTask = RunScopedTimedAsync(
            (services, ct) => BuildComingUpSectionAsync(services, releaseRegion, contentLocale, ct),
            cancellationToken);

        var topRatedTask = RunScopedTimedAsync(
            (services, ct) => BuildTopRatedSectionAsync(services, criteria, contentLocale, ct),
            cancellationToken);

        var newReleasesTask = RunScopedTimedAsync(
            (services, ct) => HomeSectionBuilders.BuildDiscoverySectionAsync(
                HomeSectionType.NewReleases,
                "New Releases",
                services.GetRequiredService<IDiscoveryService>()
                    .GetNewReleasesAsync(discoveryCriteria, contentLocale, ct),
                criteria,
                ct),
            cancellationToken);

        await Task.WhenAll(
            recommendationSectionsTask,
            weeklySectionsTask,
            comingUpTask,
            topRatedTask,
            newReleasesTask);

        var (recommendationSections, recommendedForYouMs) = await recommendationSectionsTask;
        var isPersonalized = recommendationSections.Any(section => section.Key == RecommendedForYouKey);

        var (comingUpSection, comingUpMs) = await comingUpTask;
        var (weeklySections, weeklySectionsMs) = await weeklySectionsTask;
        var hotThisWeekSection = weeklySections.HotThisWeek;
        var trendingSection = weeklySections.Trending;
        var hotThisWeekMs = weeklySectionsMs;
        var trendingMs = weeklySectionsMs;
        var (topRatedSection, topRatedMs) = await topRatedTask;
        var (newReleasesSection, newReleasesMs) = await newReleasesTask;

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>
        {
            [HomeSectionType.HotThisWeek] = hotThisWeekSection,
            [HomeSectionType.Trending] = trendingSection,
            [HomeSectionType.TopRated] = topRatedSection,
            [HomeSectionType.NewReleases] = newReleasesSection
        };

        if (comingUpSection.Items.Count > 0)
        {
            sectionsByType[HomeSectionType.ComingUp] = comingUpSection;
        }

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

        var cacheWriteStopwatch = Stopwatch.StartNew();
        await cacheService.SetAsync(
            cacheKey,
            new HomeCacheEntry { Result = result },
            TimeSpan.FromMinutes(_options.CacheTtlMinutes),
            cancellationToken);
        cacheWriteStopwatch.Stop();
        totalStopwatch.Stop();

        HomeServiceLogMessages.LogCacheMiss(
            logger,
            "MISS",
            totalStopwatch.ElapsedMilliseconds,
            cacheLookupStopwatch.ElapsedMilliseconds,
            cacheWriteStopwatch.ElapsedMilliseconds,
            hotThisWeekMs,
            recommendedForYouMs,
            comingUpMs,
            trendingMs,
            topRatedMs,
            newReleasesMs);

        return result;
    }

    public async Task<HomeBrowseResult> GetHomeBrowseAsync(
        HomeCriteria criteria,
        string contentLocale,
        string? releaseRegion = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        ValidateCriteria(criteria);
        _ = CurrentUserGuard.RequireUserId(currentUser);

        var heroSize = Math.Min(_options.HeroSectionSize, criteria.SectionSize);
        var discoveryCriteria = new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize);

        var weeklySectionsTask = RunScopedTimedAsync(
            (services, ct) => BuildWeeklyHeroAndTrendingSectionsAsync(
                services,
                criteria,
                heroSize,
                contentLocale,
                ct),
            cancellationToken);

        var topRatedTask = RunScopedTimedAsync(
            (services, ct) => BuildTopRatedSectionAsync(services, criteria, contentLocale, ct),
            cancellationToken);

        var newReleasesTask = RunScopedTimedAsync(
            (services, ct) => HomeSectionBuilders.BuildDiscoverySectionAsync(
                HomeSectionType.NewReleases,
                "New Releases",
                services.GetRequiredService<IDiscoveryService>()
                    .GetNewReleasesAsync(discoveryCriteria, contentLocale, ct),
                criteria,
                ct),
            cancellationToken);

        await Task.WhenAll(weeklySectionsTask, topRatedTask, newReleasesTask);

        var (weeklySections, weeklySectionsMs) = await weeklySectionsTask;
        var hotThisWeekSection = weeklySections.HotThisWeek;
        var trendingSection = weeklySections.Trending;
        var hotThisWeekMs = weeklySectionsMs;
        var trendingMs = weeklySectionsMs;
        var (topRatedSection, topRatedMs) = await topRatedTask;
        var (newReleasesSection, newReleasesMs) = await newReleasesTask;

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>
        {
            [HomeSectionType.HotThisWeek] = hotThisWeekSection,
            [HomeSectionType.Trending] = trendingSection,
            [HomeSectionType.TopRated] = topRatedSection,
            [HomeSectionType.NewReleases] = newReleasesSection
        };

        var orderedSections = BuildOrderedSections(sectionsByType, BrowseSectionOrder);
        totalStopwatch.Stop();

        HomeServiceLogMessages.LogBrowse(
            logger,
            totalStopwatch.ElapsedMilliseconds,
            hotThisWeekMs,
            trendingMs,
            topRatedMs,
            newReleasesMs,
            orderedSections.Count);

        return new HomeBrowseResult(orderedSections, DateTime.UtcNow);
    }

    public async Task<HomePersonalizedResult> GetHomePersonalizedAsync(
        HomeCriteria criteria,
        string contentLocale,
        string? releaseRegion = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        ValidateCriteria(criteria);
        _ = CurrentUserGuard.RequireUserId(currentUser);

        var heroSize = Math.Min(_options.HeroSectionSize, criteria.SectionSize);
        var recommendationLimit = RecommendationCandidateLimit(criteria.SectionSize);

        var recommendationSectionsTask = RunScopedTimedAsync(
            (services, ct) => services
                .GetRequiredService<IRecommendationService>()
                .GetHomeRecommendationsForCurrentUserAsync(
                    includeColdStartDiscoverySections: false,
                    contentLocale,
                    MapRecommendationContentType(criteria.Type),
                    recommendationLimit,
                    diversify: false,
                    ct),
            cancellationToken);

        var comingUpTask = RunScopedTimedAsync(
            (services, ct) => BuildComingUpSectionAsync(services, releaseRegion, contentLocale, ct),
            cancellationToken);

        var hotThisWeekDedupTask = RunScopedTimedAsync(
            (services, ct) => BuildHotThisWeekSectionAsync(
                services,
                criteria,
                heroSize,
                contentLocale,
                ct),
            cancellationToken);

        await Task.WhenAll(recommendationSectionsTask, comingUpTask, hotThisWeekDedupTask);

        var (recommendationSections, recommendedForYouMs) = await recommendationSectionsTask;
        var isPersonalized = recommendationSections.Any(section => section.Key == RecommendedForYouKey);
        var (comingUpSection, comingUpMs) = await comingUpTask;
        var (hotThisWeekSection, hotThisWeekDedupMs) = await hotThisWeekDedupTask;

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>();

        if (comingUpSection.Items.Count > 0)
        {
            sectionsByType[HomeSectionType.ComingUp] = comingUpSection;
        }

        if (isPersonalized)
        {
            var recommendedSection = BuildRecommendedSection(
                recommendationSections,
                criteria,
                hotThisWeekSection);

            if (recommendedSection is not null)
            {
                sectionsByType[HomeSectionType.RecommendedForYou] = recommendedSection;
            }
        }

        var orderedSections = BuildOrderedSections(sectionsByType, PersonalizedSectionOrder);
        totalStopwatch.Stop();

        HomeServiceLogMessages.LogPersonalized(
            logger,
            totalStopwatch.ElapsedMilliseconds,
            hotThisWeekDedupMs,
            comingUpMs,
            recommendedForYouMs,
            isPersonalized,
            orderedSections.Count);

        return new HomePersonalizedResult(orderedSections, isPersonalized, DateTime.UtcNow);
    }

    private string ResolveReleaseRegion(string? releaseRegion)
    {
        if (!string.IsNullOrWhiteSpace(releaseRegion))
        {
            return WatchProviderRegionValidator.Normalize(releaseRegion);
        }

        return WatchProviderRegionValidator.Normalize(_releaseRegionOptions.DefaultRegion);
    }

    private async Task<HomeSection> BuildComingUpSectionAsync(
        IServiceProvider services,
        string? releaseRegion,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var items = await services
            .GetRequiredService<IGetHomeComingUpService>()
            .GetItemsAsync(_options.ComingUpSectionSize, releaseRegion, contentLocale, cancellationToken);

        var homeItems = items
            .Select(HomeMapper.FromUpcomingItem)
            .ToList();

        return new HomeSection(
            HomeSectionType.ComingUp,
            "Coming Up",
            homeItems,
            0);
    }

    private static async Task<HomeSection> BuildTopRatedSectionAsync(
        IServiceProvider services,
        HomeCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var items = await services
            .GetRequiredService<IHomeTopRatedService>()
            .GetItemsAsync(criteria.Type, criteria.SectionSize, contentLocale, cancellationToken);

        var homeItems = HomeSectionBuilders.DeduplicateItems(
            items.Select(HomeMapper.FromSearchItem),
            criteria.SectionSize);

        return new HomeSection(
            HomeSectionType.TopRated,
            "Top Rated",
            HomeSectionBuilders.FilterByType(homeItems, criteria.Type),
            0);
    }

    private sealed record WeeklyHeroAndTrendingSections(HomeSection HotThisWeek, HomeSection Trending);

    private static async Task<WeeklyHeroAndTrendingSections> BuildWeeklyHeroAndTrendingSectionsAsync(
        IServiceProvider services,
        HomeCriteria criteria,
        int heroSize,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var heroItems = await services
            .GetRequiredService<IHotThisWeekService>()
            .GetItemsAsync(criteria.Type, heroSize, contentLocale, cancellationToken);

        var trendingItems = await services
            .GetRequiredService<IHomeWeeklyTrendingSectionService>()
            .GetTrendingItemsAsync(
                criteria.Type,
                heroItems,
                heroSize,
                criteria.SectionSize,
                contentLocale,
                cancellationToken);

        var hotThisWeek = BuildHotThisWeekSection(criteria, heroSize, heroItems);
        var trending = BuildHomeTrendingSection(criteria, trendingItems);

        return new WeeklyHeroAndTrendingSections(hotThisWeek, trending);
    }

    private static async Task<HomeSection> BuildHotThisWeekSectionAsync(
        IServiceProvider services,
        HomeCriteria criteria,
        int heroSize,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var items = await services
            .GetRequiredService<IHotThisWeekService>()
            .GetItemsAsync(criteria.Type, heroSize, contentLocale, cancellationToken);

        return BuildHotThisWeekSection(criteria, heroSize, items);
    }

    private static HomeSection BuildHotThisWeekSection(
        HomeCriteria criteria,
        int heroSize,
        IReadOnlyList<SearchItem> items)
    {
        var homeItems = HomeSectionBuilders.DeduplicateItems(
            items.Select(HomeMapper.FromSearchItem),
            heroSize);

        return new HomeSection(
            HomeSectionType.HotThisWeek,
            "Hot This Week",
            HomeSectionBuilders.FilterByType(homeItems, criteria.Type),
            0);
    }

    private static HomeSection BuildHomeTrendingSection(
        HomeCriteria criteria,
        IReadOnlyList<SearchItem> items)
    {
        var homeItems = HomeSectionBuilders.DeduplicateItems(
            items.Select(HomeMapper.FromSearchItem),
            criteria.SectionSize);

        return new HomeSection(
            HomeSectionType.Trending,
            "Trending Now",
            HomeSectionBuilders.FilterByType(homeItems, criteria.Type),
            0);
    }

    private HomeSection? BuildRecommendedSection(
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

        var heroIds = hotThisWeekSection?.Items
            .Select(item => item.Id)
            .ToHashSet() ?? [];

        var withoutHero = heroIds.Count == 0
            ? recommended.Items
            : recommended.Items.Where(item => !heroIds.Contains(item.Id)).ToList();

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(
            withoutHero,
            _recommendationOptions);

        var items = HomeSectionBuilders.DeduplicateItems(
            diversified.Select(HomeMapper.FromRecommendationItem),
            diversified.Count);

        var filteredItems = HomeSectionBuilders.FilterByType(items, criteria.Type)
            .Take(criteria.SectionSize)
            .ToList();

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

    private async Task<(T Result, long ElapsedMs)> RunScopedTimedAsync<T>(
        Func<IServiceProvider, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var scope = scopeFactory.CreateScope();
        var result = await operation(scope.ServiceProvider, cancellationToken);
        stopwatch.Stop();
        return (result, stopwatch.ElapsedMilliseconds);
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

    private int RecommendationCandidateLimit(int sectionSize) =>
        sectionSize + _options.HeroSectionSize + Math.Max(0, _recommendationOptions.HomeRecommendationSurplus);

    private static RecommendationContentType MapRecommendationContentType(SearchContentType type) =>
        type switch
        {
            SearchContentType.Movie => RecommendationContentType.Movie,
            SearchContentType.Tv => RecommendationContentType.Tv,
            _ => RecommendationContentType.All
        };

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

