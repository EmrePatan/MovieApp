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
using MovieApp.Application.Services.Search;
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

        var discoveryCriteria = new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize);
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
        var trendingTask = RunScopedAsync(
            (services, ct) => BuildDiscoverySectionAsync(
                HomeSectionType.Trending,
                "Trending",
                services.GetRequiredService<IDiscoveryService>().GetTrendingAsync(discoveryCriteria, ct),
                criteria,
                ct),
            cancellationToken);
        var popularTask = RunScopedAsync(
            (services, ct) => BuildDiscoverySectionAsync(
                HomeSectionType.Popular,
                "Popular",
                services.GetRequiredService<IDiscoveryService>().GetPopularAsync(discoveryCriteria, ct),
                criteria,
                ct),
            cancellationToken);
        var newReleasesTask = RunScopedAsync(
            (services, ct) => BuildDiscoverySectionAsync(
                HomeSectionType.NewReleases,
                "New Releases",
                services.GetRequiredService<IDiscoveryService>().GetNewReleasesAsync(discoveryCriteria, ct),
                criteria,
                ct),
            cancellationToken);
        var topRatedTask = RunScopedAsync(
            (services, ct) => BuildDiscoverySectionAsync(
                HomeSectionType.TopRated,
                "Top Rated",
                services.GetRequiredService<IDiscoveryService>().GetTopRatedAsync(discoveryCriteria, ct),
                criteria,
                ct),
            cancellationToken);
        var genreSectionsTask = RunScopedAsync(
            (services, ct) => BuildGenreSectionsAsync(
                services.GetRequiredService<IDiscoveryService>(),
                criteria,
                ct),
            cancellationToken);

        await Task.WhenAll(
            recommendationSectionsTask,
            continueWatchingTask,
            trendingTask,
            popularTask,
            newReleasesTask,
            topRatedTask,
            genreSectionsTask);

        var recommendationSections = await recommendationSectionsTask;
        var isPersonalized = recommendationSections.Any(section => section.Key == RecommendedForYouKey);

        var sectionsByType = new Dictionary<HomeSectionType, HomeSection>
        {
            [HomeSectionType.ContinueWatching] = await continueWatchingTask,
            [HomeSectionType.Trending] = await trendingTask,
            [HomeSectionType.Popular] = await popularTask,
            [HomeSectionType.NewReleases] = await newReleasesTask,
            [HomeSectionType.TopRated] = await topRatedTask
        };

        if (isPersonalized)
        {
            AddRecommendationSections(sectionsByType, recommendationSections, criteria);
        }

        var genreSections = await genreSectionsTask;
        var sectionOrder = isPersonalized ? PersonalizedSectionOrder : ColdStartSectionOrder;
        var orderedSections = BuildOrderedSections(sectionsByType, genreSections, sectionOrder);

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
        var homeItems = DeduplicateItems(items.Select(HomeMapper.FromContinueWatchingItem), criteria.SectionSize);

        return new HomeSection(
            HomeSectionType.ContinueWatching,
            "Continue Watching",
            homeItems,
            0);
    }

    private static async Task<HomeSection> BuildDiscoverySectionAsync(
        HomeSectionType sectionType,
        string title,
        Task<PaginatedResult<SearchItem>> discoveryTask,
        HomeCriteria criteria,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var discovery = await discoveryTask;
        var items = DeduplicateItems(discovery.Items.Select(HomeMapper.FromSearchItem), criteria.SectionSize);

        return new HomeSection(sectionType, title, FilterByType(items, criteria.Type), 0);
    }

    private async Task<List<HomeSection>> BuildGenreSectionsAsync(
        IDiscoveryService discoveryService,
        HomeCriteria criteria,
        CancellationToken cancellationToken)
    {
        var discoveryCriteria = new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize);
        var sections = new List<HomeSection>();

        foreach (var genreName in _options.GenreSections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discovery = await discoveryService.GetByGenreAsync(genreName, discoveryCriteria, cancellationToken);
            var items = DeduplicateItems(discovery.Items.Select(HomeMapper.FromSearchItem), criteria.SectionSize);
            var filteredItems = FilterByType(items, criteria.Type);

            if (filteredItems.Count == 0)
            {
                continue;
            }

            sections.Add(new HomeSection(HomeSectionType.Genre, genreName, filteredItems, 0));
        }

        return sections;
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

        var items = DeduplicateItems(section.Items.Select(HomeMapper.FromRecommendationItem), criteria.SectionSize);
        var filteredItems = FilterByType(items, criteria.Type);

        if (filteredItems.Count == 0)
        {
            return false;
        }

        homeSection = new HomeSection(sectionType.Value, section.Title, filteredItems, 0);
        return true;
    }

    private static List<HomeSection> BuildOrderedSections(
        Dictionary<HomeSectionType, HomeSection> sectionsByType,
        List<HomeSection> genreSections,
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

    private static List<HomeItem> DeduplicateItems(IEnumerable<HomeItem> items, int sectionSize)
    {
        var seen = new HashSet<Guid>();
        var result = new List<HomeItem>();

        foreach (var item in items)
        {
            if (!seen.Add(item.Id))
            {
                continue;
            }

            result.Add(item);

            if (result.Count >= sectionSize)
            {
                break;
            }
        }

        return result;
    }

    private static List<HomeItem> FilterByType(IReadOnlyList<HomeItem> items, SearchContentType type) =>
        type switch
        {
            SearchContentType.Movie => items.Where(item => item.ContentType == "movie").ToList(),
            SearchContentType.Tv => items.Where(item => item.ContentType == "tv").ToList(),
            _ => items.ToList()
        };

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
