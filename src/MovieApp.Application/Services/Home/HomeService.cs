using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Home;

public sealed class HomeService(
    ICurrentUser currentUser,
    IServiceScopeFactory scopeFactory,
    ICacheService cacheService,
    IOptions<HomeOptions> options) : IHomeService
{
    private const string RecommendedForYouKey = "recommended-for-you";
    private const string BecauseYouWatchedKey = "because-you-watched";
    private const string BasedOnFavoritesKey = "similar-to-favorites";

    private static readonly HomeSectionType[] PersonalizedSectionOrder =
    [
        HomeSectionType.ContinueWatching,
        HomeSectionType.RecommendedForYou,
        HomeSectionType.BecauseYouWatched,
        HomeSectionType.BasedOnFavorites,
        HomeSectionType.Trending,
        HomeSectionType.Popular,
        HomeSectionType.NewReleases,
        HomeSectionType.TopRated
    ];

    private static readonly HomeSectionType[] ColdStartSectionOrder =
    [
        HomeSectionType.ContinueWatching,
        HomeSectionType.Trending,
        HomeSectionType.Popular,
        HomeSectionType.NewReleases,
        HomeSectionType.TopRated
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

        var recommendationSectionsTask = RunScopedAsync(
            (services, ct) => services
                .GetRequiredService<IRecommendationService>()
                .GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false, ct),
            cancellationToken);
        var continueWatchingTask = RunScopedAsync(
            (services, ct) => BuildContinueWatchingSectionAsync(
                services.GetRequiredService<IWatchHistoryService>(),
                criteria,
                ct),
            cancellationToken);
        var globalSectionsTask = RunScopedAsync(
            (services, ct) => services
                .GetRequiredService<IHomeGlobalSectionsProvider>()
                .GetOrLoadAsync(criteria, ct),
            cancellationToken);

        await Task.WhenAll(
            recommendationSectionsTask,
            continueWatchingTask,
            globalSectionsTask);

        var recommendationSections = await recommendationSectionsTask;
        var isPersonalized = recommendationSections.Any(section => section.Key == RecommendedForYouKey);
        var globalSections = await globalSectionsTask;

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>
        {
            [HomeSectionType.ContinueWatching] = await continueWatchingTask,
            [HomeSectionType.Trending] = globalSections.Trending,
            [HomeSectionType.Popular] = globalSections.Popular,
            [HomeSectionType.NewReleases] = globalSections.NewReleases,
            [HomeSectionType.TopRated] = globalSections.TopRated
        };

        if (isPersonalized)
        {
            AddRecommendationSections(sectionsByType, recommendationSections, criteria);
        }

        var sectionOrder = isPersonalized ? PersonalizedSectionOrder : ColdStartSectionOrder;
        var orderedSections = BuildOrderedSections(sectionsByType, globalSections.GenreSections, sectionOrder);

        var result = new HomeResult(orderedSections, isPersonalized);

        await cacheService.SetAsync(
            cacheKey,
            new HomeCacheEntry { Result = result },
            TimeSpan.FromMinutes(_options.CacheTtlMinutes),
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

    private static async Task<HomeSection> BuildContinueWatchingSectionAsync(
        IWatchHistoryService watchHistoryService,
        HomeCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (criteria.Type == SearchContentType.Movie)
        {
            return CreateEmptySection(HomeSectionType.ContinueWatching, "Continue Watching");
        }

        var items = await watchHistoryService.GetContinueWatchingAsync(criteria.SectionSize, cancellationToken);
        var homeItems = HomeSectionBuilders.DeduplicateItems(
            items.Select(HomeMapper.FromContinueWatchingItem),
            criteria.SectionSize);

        return new HomeSection(
            HomeSectionType.ContinueWatching,
            "Continue Watching",
            homeItems,
            0);
    }

    private static void AddRecommendationSections(
        Dictionary<HomeSectionType, HomeSection> sectionsByType,
        IReadOnlyList<RecommendationSection> recommendationSections,
        HomeCriteria criteria)
    {
        foreach (var section in recommendationSections)
        {
            if (!TryMapRecommendationSection(section, criteria, out var homeSection))
            {
                continue;
            }

            sectionsByType[homeSection.Type] = homeSection;
        }
    }

    private static bool TryMapRecommendationSection(
        RecommendationSection section,
        HomeCriteria criteria,
        out HomeSection homeSection)
    {
        homeSection = CreateEmptySection(HomeSectionType.RecommendedForYou, section.Title);

        var sectionType = section.Key switch
        {
            RecommendedForYouKey => HomeSectionType.RecommendedForYou,
            BecauseYouWatchedKey => HomeSectionType.BecauseYouWatched,
            BasedOnFavoritesKey => HomeSectionType.BasedOnFavorites,
            _ => (HomeSectionType?)null
        };

        if (sectionType is null)
        {
            return false;
        }

        var items = HomeSectionBuilders.DeduplicateItems(
            section.Items.Select(HomeMapper.FromRecommendationItem),
            criteria.SectionSize);
        var filteredItems = HomeSectionBuilders.FilterByType(items, criteria.Type);

        if (filteredItems.Count == 0)
        {
            return false;
        }

        homeSection = new HomeSection(sectionType.Value, section.Title, filteredItems, 0);
        return true;
    }

    private static List<HomeSection> BuildOrderedSections(
        Dictionary<HomeSectionType, HomeSection> sectionsByType,
        IReadOnlyList<HomeSection> genreSections,
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

        foreach (var genreSection in genreSections)
        {
            orderedSections.Add(genreSection with { DisplayOrder = displayOrder++ });
        }

        return orderedSections;
    }

    private static HomeSection CreateEmptySection(HomeSectionType type, string title) =>
        new(type, title, [], 0);

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
