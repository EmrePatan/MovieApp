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
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Home;

public sealed class HomeService(
    ICurrentUser currentUser,
    IServiceScopeFactory scopeFactory,
    ICacheService cacheService,
    ISearchRefreshLockService refreshLockService,
    HomeLoadCoordinator loadCoordinator,
    IOptions<HomeOptions> options,
    IOptions<ReleaseRegionOptions> releaseRegionOptions,
    IOptions<RecommendationOptions> recommendationOptions,
    ILogger<HomeService> logger) : IHomeService
{
    private const string RecommendedForYouKey = "recommended-for-you";

    private static readonly TimeSpan HomeRefreshLockDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CachePollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly int MaxCachePollAttempts = 50;

    /// <summary>
    /// Max scored rows Home will request for the Recommended For You rail.
    /// <see cref="RecommendationOptions.MaximumCandidates"/> (500) stays the SQL fetch budget
    /// for paginated recommendations. Using it as this list size hydrated and cached ~500
    /// items on every Home miss and pushed small instances toward their memory ceiling.
    /// 80 still covers the largest home rail (section size 20) after hero dedup plus the
    /// genre, collection, and franchise caps that fill the rail from the scored pool.
    /// </summary>
    private const int HomeScoredPoolSize = 80;

    private static readonly HomeSectionType[] HomeSectionOrder =
    [
        HomeSectionType.HotThisWeek,
        HomeSectionType.RecommendedForYou,
        HomeSectionType.ComingUp,
        HomeSectionType.Trending,
        HomeSectionType.OnTvThisWeek,
        HomeSectionType.NowInTheaters
    ];

    private static readonly HomeSectionType[] BrowseSectionOrder =
    [
        HomeSectionType.HotThisWeek,
        HomeSectionType.Trending,
        HomeSectionType.OnTvThisWeek,
        HomeSectionType.NowInTheaters
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
            HomeStampedePerfAmbient.RecordHomeCacheHit();
            totalStopwatch.Stop();
            HomeServiceLogMessages.LogCacheHit(
                logger,
                "HIT",
                totalStopwatch.ElapsedMilliseconds,
                cacheLookupStopwatch.ElapsedMilliseconds);
            return cached.Result;
        }

        HomeStampedePerfAmbient.RecordHomeCacheMiss();

        var inFlight = loadCoordinator.TryGetInFlight(cacheKey);
        if (inFlight is not null)
        {
            totalStopwatch.Stop();
            return await inFlight.WaitAsync(cancellationToken);
        }

        var lockKey = HomeCacheLockKeys.Create(cacheKey);
        var lockHandle = await refreshLockService.TryAcquireAsync(
            lockKey,
            HomeRefreshLockDuration,
            cancellationToken);

        if (lockHandle is null)
        {
            inFlight = loadCoordinator.TryGetInFlight(cacheKey);
            if (inFlight is not null)
            {
                totalStopwatch.Stop();
                return await inFlight.WaitAsync(cancellationToken);
            }

            var waited = await WaitForCachedHomeAsync(cacheKey, cancellationToken);
            if (waited is not null)
            {
                totalStopwatch.Stop();
                return waited.Result;
            }
        }

        try
        {
            cached = await cacheService.GetAsync<HomeCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                totalStopwatch.Stop();
                return cached.Result;
            }

            var result = await loadCoordinator.RunInFlightAsync(
                cacheKey,
                () => BuildAndCacheHomeOnMissAsync(
                    criteria,
                    contentLocale,
                    releaseRegion,
                    releaseRegionKey,
                    cacheKey,
                    cacheLookupStopwatch.ElapsedMilliseconds,
                    totalStopwatch,
                    cancellationToken));

            totalStopwatch.Stop();
            return result;
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

    private async Task<HomeResult> BuildAndCacheHomeOnMissAsync(
        HomeCriteria criteria,
        string contentLocale,
        string? releaseRegion,
        string releaseRegionKey,
        string cacheKey,
        long initialCacheLookupMs,
        Stopwatch totalStopwatch,
        CancellationToken cancellationToken)
    {
        var recheck = await cacheService.GetAsync<HomeCacheEntry>(cacheKey, cancellationToken);
        if (recheck is not null)
        {
            return recheck.Result;
        }

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
                    includeBecauseYouWatched: false,
                    ct),
            cancellationToken);

        var weeklySectionsTask = RunScopedTimedAsync(
            (services, ct) =>
            {
                HomeStampedePerfAmbient.RecordWeeklyTrendingBuild();
                return BuildWeeklyHeroAndTrendingSectionsAsync(
                    services,
                    criteria,
                    heroSize,
                    contentLocale,
                    ct);
            },
            cancellationToken);

        var comingUpTask = RunScopedTimedAsync(
            (services, ct) =>
            {
                HomeStampedePerfAmbient.RecordComingUpBuild();
                return BuildComingUpSectionAsync(services, releaseRegion, contentLocale, ct);
            },
            cancellationToken);

        var onTvTask = RunScopedTimedAsync(
            (services, ct) =>
            {
                HomeStampedePerfAmbient.RecordOnTvBuild();
                return BuildOnTvThisWeekSectionAsync(services, criteria, contentLocale, ct);
            },
            cancellationToken);

        var nowPlayingTask = RunScopedTimedAsync(
            (services, ct) =>
            {
                HomeStampedePerfAmbient.RecordNowInTheatersBuild();
                return BuildNowInTheatersSectionAsync(
                    services,
                    criteria,
                    releaseRegionKey,
                    contentLocale,
                    ct);
            },
            cancellationToken);

        await Task.WhenAll(
            recommendationSectionsTask,
            weeklySectionsTask,
            comingUpTask,
            onTvTask,
            nowPlayingTask);

        var (recommendationSections, recommendedForYouMs) = await recommendationSectionsTask;
        var isPersonalized = recommendationSections.Any(section => section.Key == RecommendedForYouKey);

        var (comingUpSection, comingUpMs) = await comingUpTask;
        var (weeklySections, weeklySectionsMs) = await weeklySectionsTask;
        var hotThisWeekSection = weeklySections.HotThisWeek;
        var trendingSection = weeklySections.Trending;
        var hotThisWeekMs = weeklySectionsMs;
        var trendingMs = weeklySectionsMs;
        var (onTvSection, onTvMs) = await onTvTask;
        var (nowPlayingSection, nowPlayingMs) = await nowPlayingTask;

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>
        {
            [HomeSectionType.HotThisWeek] = hotThisWeekSection,
            [HomeSectionType.Trending] = trendingSection,
            [HomeSectionType.OnTvThisWeek] = onTvSection,
            [HomeSectionType.NowInTheaters] = nowPlayingSection
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

        HomeStampedePerfAmbient.RecordHomeBuildCompleted();

        HomeServiceLogMessages.LogCacheMiss(
            logger,
            "MISS",
            totalStopwatch.ElapsedMilliseconds,
            initialCacheLookupMs,
            cacheWriteStopwatch.ElapsedMilliseconds,
            hotThisWeekMs,
            recommendedForYouMs,
            comingUpMs,
            trendingMs,
            onTvMs,
            nowPlayingMs);

        return result;
    }

    private async Task<HomeCacheEntry?> WaitForCachedHomeAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCachePollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inFlight = loadCoordinator.TryGetInFlight(cacheKey);
            if (inFlight is not null)
            {
                await inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);
                var cachedAfterInFlight = await cacheService.GetAsync<HomeCacheEntry>(cacheKey, cancellationToken);
                if (cachedAfterInFlight is not null)
                {
                    return cachedAfterInFlight;
                }
            }

            await Task.Delay(CachePollInterval, cancellationToken);

            var cached = await cacheService.GetAsync<HomeCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }

        return null;
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
        var releaseRegionKey = ResolveReleaseRegion(releaseRegion);

        var weeklySectionsTask = RunScopedTimedAsync(
            (services, ct) => BuildWeeklyHeroAndTrendingSectionsAsync(
                services,
                criteria,
                heroSize,
                contentLocale,
                ct),
            cancellationToken);

        var onTvTask = RunScopedTimedAsync(
            (services, ct) => BuildOnTvThisWeekSectionAsync(services, criteria, contentLocale, ct),
            cancellationToken);

        var nowPlayingTask = RunScopedTimedAsync(
            (services, ct) => BuildNowInTheatersSectionAsync(
                services,
                criteria,
                releaseRegionKey,
                contentLocale,
                ct),
            cancellationToken);

        await Task.WhenAll(weeklySectionsTask, onTvTask, nowPlayingTask);

        var (weeklySections, weeklySectionsMs) = await weeklySectionsTask;
        var hotThisWeekSection = weeklySections.HotThisWeek;
        var trendingSection = weeklySections.Trending;
        var hotThisWeekMs = weeklySectionsMs;
        var trendingMs = weeklySectionsMs;
        var (onTvSection, onTvMs) = await onTvTask;
        var (nowPlayingSection, nowPlayingMs) = await nowPlayingTask;

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>
        {
            [HomeSectionType.HotThisWeek] = hotThisWeekSection,
            [HomeSectionType.Trending] = trendingSection,
            [HomeSectionType.OnTvThisWeek] = onTvSection,
            [HomeSectionType.NowInTheaters] = nowPlayingSection
        };

        var orderedSections = BuildOrderedSections(sectionsByType, BrowseSectionOrder);
        totalStopwatch.Stop();

        HomeServiceLogMessages.LogBrowse(
            logger,
            totalStopwatch.ElapsedMilliseconds,
            hotThisWeekMs,
            trendingMs,
            onTvMs,
            nowPlayingMs,
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
                    includeBecauseYouWatched: false,
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
            0,
            ComingUpSources.ForYou);
    }

    private static async Task<HomeSection> BuildOnTvThisWeekSectionAsync(
        IServiceProvider services,
        HomeCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (criteria.Type == SearchContentType.Movie)
        {
            return new HomeSection(HomeSectionType.OnTvThisWeek, "On TV This Week", [], 0);
        }

        var result = await services
            .GetRequiredService<IOnTvThisWeekService>()
            .GetOnTvThisWeekAsync(
                new OnTvThisWeekCriteria(
                    1,
                    criteria.SectionSize,
                    OnTvThisWeekPresentationIntent.HomeRail),
                contentLocale,
                cancellationToken);

        var homeItems = HomeSectionBuilders.DeduplicateItems(
            result.Items.Select(HomeMapper.FromSearchItem),
            criteria.SectionSize);

        return new HomeSection(
            HomeSectionType.OnTvThisWeek,
            "On TV This Week",
            HomeSectionBuilders.FilterByType(homeItems, criteria.Type),
            0);
    }

    private static async Task<HomeSection> BuildNowInTheatersSectionAsync(
        IServiceProvider services,
        HomeCriteria criteria,
        string releaseRegion,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (criteria.Type == SearchContentType.Tv)
        {
            return new HomeSection(HomeSectionType.NowInTheaters, "Now in Theaters", [], 0);
        }

        var result = await services
            .GetRequiredService<INowInTheatersService>()
            .GetNowInTheatersAsync(
                new NowInTheatersCriteria(releaseRegion, 1, criteria.SectionSize),
                contentLocale,
                cancellationToken);

        var homeItems = HomeSectionBuilders.DeduplicateItems(
            result.Items.Select(HomeMapper.FromSearchItem),
            criteria.SectionSize);

        return new HomeSection(
            HomeSectionType.NowInTheaters,
            "Now in Theaters",
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

        var candidates = FilterRecommendationItems(withoutHero, criteria.Type);
        var ranked = PersonalizedRecommendationEngine.SelectHomeRecommended(
            candidates,
            _recommendationOptions,
            criteria.SectionSize);

        var filteredItems = HomeSectionBuilders.DeduplicateItems(
            ranked.Select(HomeMapper.FromRecommendationItem),
            criteria.SectionSize);

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

    private int RecommendationCandidateLimit(int sectionSize)
    {
        var heroWindow = sectionSize
            + Math.Max(0, _options.HeroSectionSize)
            + Math.Max(0, _recommendationOptions.HomeRecommendationSurplus);
        // HomeScoredPoolSize is the target scored pool size passed to SelectHomeRecommended
        // (not the SQL MaximumCandidates budget).
        var upper = Math.Max(heroWindow, HomeScoredPoolSize);
        return upper;
    }

    private static IReadOnlyList<RecommendationItem> FilterRecommendationItems(
        IReadOnlyList<RecommendationItem> items,
        SearchContentType type) =>
        type switch
        {
            SearchContentType.Movie => items.Where(item => item.Type == "movie").ToList(),
            SearchContentType.Tv => items.Where(item => item.Type == "tv").ToList(),
            _ => items
        };

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

