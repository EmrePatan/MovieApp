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
    private const string BecauseYouWatchedKey = "because-you-watched";

    private static readonly HomeSectionType[] PersonalizedSectionOrder =
    [
        HomeSectionType.RecommendedForYou,
        HomeSectionType.BecauseYouWatched
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

        var recommendationSections = await RunScopedAsync(
            (services, ct) => services
                .GetRequiredService<IRecommendationService>()
                .GetHomeRecommendationsForCurrentUserAsync(includeColdStartDiscoverySections: false, ct),
            cancellationToken);

        var isPersonalized = recommendationSections.Any(section => section.Key == RecommendedForYouKey);
        List<HomeSection> orderedSections;

        if (isPersonalized)
        {
            var sectionsByType = new Dictionary<HomeSectionType, HomeSection>();
            AddRecommendationSections(sectionsByType, recommendationSections, criteria);
            orderedSections = BuildOrderedSections(sectionsByType, PersonalizedSectionOrder);
        }
        else
        {
            var trending = await RunScopedAsync(
                (services, ct) => HomeSectionBuilders.BuildDiscoverySectionAsync(
                    HomeSectionType.Trending,
                    "Trending",
                    services.GetRequiredService<IDiscoveryService>()
                        .GetTrendingAsync(new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize), ct),
                    criteria,
                    ct),
                cancellationToken);

            orderedSections = trending.Items.Count > 0
                ? [trending with { DisplayOrder = 1 }]
                : [];
        }

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
