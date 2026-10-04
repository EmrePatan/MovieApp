using MovieApp.Application.Services.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
using MovieApp.Infrastructure.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Home;

public sealed class HomeServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetHomeAsyncReturnsCachedResultOnCacheHit()
    {
        var cachedResult = new HomeResult(
            [new HomeSection(HomeSectionType.Popular, "Popular", [CreateHomeItem("movie", 1)], 1)],
            false);

        var cache = new FakeCacheService(cachedResult);
        var service = CreateService(
            cache: cache,
            recommendationService: new FakeRecommendationService([]),
            discoveryService: new FakeDiscoveryService(),
            watchHistoryService: new FakeWatchHistoryService([]));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(cache.WasRead);
        Assert.False(cache.WasWritten);
        Assert.Equal(cachedResult, result);
    }

    [Fact]
    public async Task GetHomeAsync_IsolatesCacheByReleaseRegion()
    {
        var cache = new SharedHomeCacheService();
        var service = CreateService(
            cache: cache,
            recommendationService: new FakeRecommendationService([]),
            discoveryService: new FakeDiscoveryService(),
            watchHistoryService: new FakeWatchHistoryService([]));
        var criteria = new HomeCriteria(SearchContentType.All, 2);

        await service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates, "US");
        await service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates, "TR");
        await service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates, "us");

        Assert.Equal(2, cache.HomeSetCount);
    }

    [Fact]
    public async Task GetHomeAsyncBuildsColdStartSectionsInOrder()
    {
        var cache = new FakeCacheService();
        var discovery = new FakeDiscoveryService();
        var service = CreateService(
            cache: cache,
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("popular", "Popular", []),
                new RecommendationSection("trending", "Trending", []),
                new RecommendationSection("top-rated", "Top Rated", [])
            ]),
            discoveryService: discovery,
            watchHistoryService: new FakeWatchHistoryService([]));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 2), ContentLocaleResolver.EnglishUnitedStates);

        Assert.False(result.IsPersonalized);
        Assert.Equal(
            [HomeSectionType.HotThisWeek, HomeSectionType.Trending, HomeSectionType.OnTvThisWeek, HomeSectionType.NowInTheaters],
            result.Sections.Select(section => section.Type).ToList());
        Assert.True(cache.WasWritten);
    }

    [Fact]
    public async Task GetHomeAsyncIncludesComingUpBetweenRecommendedAndTrendingWhenEpisodesExist()
    {
        var comingUpItems = new List<Application.Models.CatalogFollows.CatalogUpcomingItemResult>
        {
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                MovieApp.Domain.Enums.CatalogContentType.Tv,
                MovieApp.Application.Models.CatalogFollows.CatalogUpcomingKind.TvEpisode,
                "Followed Show",
                "/poster.jpg",
                new DateOnly(2026, 9, 20),
                true,
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                1,
                2,
                "Next")
        };

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)])
            ]),
            discoveryService: new FakeDiscoveryService(),
            comingUpService: new FakeHomeComingUpService(comingUpItems));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            [
                HomeSectionType.HotThisWeek,
                HomeSectionType.RecommendedForYou,
                HomeSectionType.ComingUp,
                HomeSectionType.Trending,
                HomeSectionType.OnTvThisWeek,
                HomeSectionType.NowInTheaters
            ],
            result.Sections.Select(section => section.Type).ToList());

        var comingUp = result.Sections.Single(section => section.Type == HomeSectionType.ComingUp);
        Assert.Equal(ComingUpSources.ForYou, comingUp.ComingUpSource);
        Assert.Equal("Coming Up", comingUp.Title);
        Assert.Equal("Next", comingUp.Items[0].EpisodeName);
        Assert.Equal(2, comingUp.Items[0].EpisodeNumber);
    }

    [Fact]
    public async Task GetHomeAsyncOmitsComingUpWhenNoEpisodesExist()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)])
            ]),
            discoveryService: new FakeDiscoveryService(),
            comingUpService: new FakeHomeComingUpService([]));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.ComingUp);
    }

    [Fact]
    public async Task GetHomeAsyncBuildsPersonalizedSectionsInOrder()
    {
        var discovery = new CountingDiscoveryService();
        var recommendations = new FakeRecommendationService(
        [
            new RecommendationSection("recommended-for-you", "Recommended For You",
                [CreateRecommendationItem("movie", 1)]),
            new RecommendationSection("because-you-watched", "Because You Watched",
                [CreateRecommendationItem("tv", 2)]),
            new RecommendationSection("similar-to-favorites", "Based On Your Favorites",
                [CreateRecommendationItem("movie", 3)])
        ]);
        var service = CreateService(
            recommendationService: recommendations,
            discoveryService: discovery);

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(result.IsPersonalized);
        Assert.Equal(
            [
                HomeSectionType.HotThisWeek,
                HomeSectionType.RecommendedForYou,
                HomeSectionType.Trending,
                HomeSectionType.OnTvThisWeek,
                HomeSectionType.NowInTheaters
            ],
            result.Sections.Select(section => section.Type).ToList());
        Assert.False(recommendations.LastIncludeBecauseYouWatched);
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Equal(0, discovery.PopularCallCount);
        Assert.Equal(0, discovery.NewReleasesCallCount);
        Assert.Equal(0, discovery.TopRatedCallCount);
        Assert.Equal(0, discovery.GenreCallCount);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.BecauseYouWatched);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.ContinueWatching);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.Popular);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.NewReleases);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.TopRated);
    }

    [Fact]
    public async Task GetHomeAsyncOmitsEmptySections()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)]),
                new RecommendationSection("because-you-watched", "Because You Watched", []),
                new RecommendationSection("similar-to-favorites", "Based On Your Favorites", [])
            ]),
            discoveryService: new FakeDiscoveryService(includeGenre: false),
            watchHistoryService: new FakeWatchHistoryService([]));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.BecauseYouWatched);
    }

    [Fact]
    public async Task GetHomeAsyncFiltersMovieType()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                [
                    CreateRecommendationItem("movie", 1),
                    CreateRecommendationItem("tv", 2)
                ])
            ]),
            discoveryService: new FakeDiscoveryService(),
            watchHistoryService: new FakeWatchHistoryService(
            [
                new ContinueWatchingItemResult(
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    "Breaking Bad",
                    null,
                    null,
                    null,
                    new DateOnly(2008, 1, 20),
                    9.5m,
                    1000,
                    DateTime.UtcNow)
            ]));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.Movie, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.All(
            result.Sections.SelectMany(section => section.Items),
            item => Assert.Equal("movie", item.ContentType));
    }

    [Fact]
    public async Task GetHomeAsyncDeduplicatesItemsWithinSection()
    {
        var duplicate = CreateRecommendationItem("movie", 1);
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [duplicate, duplicate, CreateRecommendationItem("movie", 2)])
            ]),
            discoveryService: new FakeDiscoveryService(),
            watchHistoryService: new FakeWatchHistoryService([]));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Equal(2, recommended.Items.Count);
        Assert.Equal(2, recommended.Items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task GetHomeAsyncRejectsInvalidSectionSize()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 21), ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task GetHomeAsyncPropagatesCancellation()
    {
        var service = CreateService(
            recommendationService: new CancellingRecommendationService());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetHomeAsync(
                new HomeCriteria(SearchContentType.All, 5),
                ContentLocaleResolver.EnglishUnitedStates,
                releaseRegion: null,
                cts.Token));
    }

    [Fact]
    public async Task GetHomeAsyncColdStartLoadsTopRatedAndNewReleasesDiscovery()
    {
        var discovery = new CountingDiscoveryService();
        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: discovery);

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.False(result.IsPersonalized);
        Assert.Equal(
            [HomeSectionType.HotThisWeek, HomeSectionType.Trending, HomeSectionType.OnTvThisWeek, HomeSectionType.NowInTheaters],
            result.Sections.Select(section => section.Type).ToList());
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Equal(0, discovery.PopularCallCount);
        Assert.Equal(0, discovery.NewReleasesCallCount);
        Assert.Equal(0, discovery.TopRatedCallCount);
        Assert.Equal(0, discovery.GenreCallCount);
        Assert.DoesNotContain(result.Sections, section => section.Type is
            HomeSectionType.Popular or
            HomeSectionType.NewReleases or
            HomeSectionType.TopRated or
            HomeSectionType.BecauseYouWatched or
            HomeSectionType.ContinueWatching);
    }

    [Fact]
    public async Task GetHomeAsyncKeepsRecommendedIndependentFromHero()
    {
        var heroItemId = Guid.Parse("11111111-1111-1111-1111-000000000001");
        var recommendedItems = Enumerable.Range(1, 10)
            .Select(seed => CreateRecommendationItem("movie", seed))
            .ToList();

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You", recommendedItems)
            ]),
            weeklySnapshotItems:
            [
                new SearchItem(
                    heroItemId,
                    "movie",
                    "Hero Title",
                    null,
                    null,
                    null,
                    null,
                    new DateOnly(2025, 1, 1),
                    9m,
                    1000,
                    2025)
            ],
            options: new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                HeroSectionSize = 5
            });

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 10), ContentLocaleResolver.EnglishUnitedStates);

        var hero = result.Sections.Single(section => section.Type == HomeSectionType.HotThisWeek);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Single(hero.Items);
        Assert.Equal(10, recommended.Items.Count);
        Assert.Contains(recommended.Items, item => item.Id == recommendedItems[0].Id);
    }

    [Fact]
    public async Task GetHomeAsyncIncludesTrendingNowSectionWithExpectedTitle()
    {
        var discovery = new CountingDiscoveryService();
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)])
            ]),
            discoveryService: discovery);

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        var trending = result.Sections.Single(section => section.Type == HomeSectionType.Trending);

        Assert.Equal("Trending Now", trending.Title);
        Assert.Equal(5, trending.Items.Count);
        Assert.Equal(0, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task GetHomeAsyncRequestsHotThisWeekItemsUsingHeroSectionSize()
    {
        var hotItems = Enumerable.Range(1, 15)
            .Select(seed => new SearchItem(
                Guid.Parse($"11111111-1111-1111-1111-{seed:D12}"),
                "movie",
                $"Hot Title {seed}",
                null,
                null,
                null,
                null,
                new DateOnly(2025, 1, 1),
                8m,
                1000,
                2025))
            .ToList();

        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: new FakeDiscoveryService(),
            weeklySnapshotItems: hotItems,
            options: new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                HeroSectionSize = 10,
            });

        var result = await service.GetHomeAsync(
            new HomeCriteria(SearchContentType.All, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        var hotThisWeek = result.Sections.Single(section => section.Type == HomeSectionType.HotThisWeek);

        Assert.Equal(10, hotThisWeek.Items.Count);
    }

    [Fact]
    public async Task GetHomeAsyncKeepsTrendingIndependentFromHotThisWeek()
    {
        var discovery = new CountingDiscoveryService();
        var heroId = Guid.Parse("11111111-1111-1111-1111-000000000099");
        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: discovery,
            weeklySnapshotItems: CreateWeeklySnapshotItems(12, heroId, "Hot Hero"));

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        var hero = result.Sections.Single(section => section.Type == HomeSectionType.HotThisWeek);
        var trending = result.Sections.Single(section => section.Type == HomeSectionType.Trending);

        Assert.Equal(5, hero.Items.Count);
        Assert.Equal(5, trending.Items.Count);
        Assert.DoesNotContain(trending.Items, item => hero.Items.Any(heroItem => heroItem.Id == item.Id && heroItem.ContentType == item.ContentType));
        Assert.Equal(0, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task GetHomeAsyncUsesCatalogTrendingFallbackWhenWeeklySnapshotMissing()
    {
        var discovery = new CountingDiscoveryService();
        var weekList = new FakeTrendingWeekListService(CreateFallbackWeekItems());
        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: discovery,
            trendingWeekListService: weekList,
            weeklySnapshotItems: []);

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(2, weekList.CallCount);
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.HotThisWeek);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.Trending);
        Assert.Equal(0, discovery.TopRatedCallCount);
        Assert.Equal(0, discovery.NewReleasesCallCount);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.OnTvThisWeek);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.NowInTheaters);
    }

    [Fact]
    public async Task GetHomeAsyncOmitsEmptyTrendingSection()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: new EmptyTrendingDiscoveryService(),
            trendingWeekListService: new FakeTrendingWeekListService([]),
            weeklySnapshotItems: []);

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.Trending);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.TopRated);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.NewReleases);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.OnTvThisWeek);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.NowInTheaters);
    }

    [Fact]
    public async Task GetHomeAsyncContinuesWhenHotThisWeekProviderFails()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)])
            ]),
            weeklySnapshotItems: [],
            trendingWeekListService: new FakeTrendingWeekListService([]),
            discoveryService: new EmptyTrendingDiscoveryService());

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.HotThisWeek);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.RecommendedForYou);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.TopRated);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.NewReleases);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.OnTvThisWeek);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.NowInTheaters);
    }

    [Fact]
    public async Task GetHomeBrowseAsyncReturnsOnlyBrowseSectionsInOrder()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)])
            ]),
            discoveryService: new FakeDiscoveryService());

        var result = await service.GetHomeBrowseAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            [HomeSectionType.HotThisWeek, HomeSectionType.Trending, HomeSectionType.OnTvThisWeek, HomeSectionType.NowInTheaters],
            result.Sections.Select(section => section.Type).ToList());
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.RecommendedForYou);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.ComingUp);
    }

    [Fact]
    public async Task GetHomeBrowseAsyncOmitsEmptySections()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: new EmptyTrendingDiscoveryService(),
            trendingWeekListService: new FakeTrendingWeekListService([]),
            weeklySnapshotItems: []);

        var result = await service.GetHomeBrowseAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.Trending);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.TopRated);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.OnTvThisWeek);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.NowInTheaters);
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncReturnsRecommendedAndComingUpForPersonalizedUser()
    {
        var comingUpItems = new List<Application.Models.CatalogFollows.CatalogUpcomingItemResult>
        {
            new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                MovieApp.Domain.Enums.CatalogContentType.Tv,
                MovieApp.Application.Models.CatalogFollows.CatalogUpcomingKind.TvEpisode,
                "Followed Show",
                "/poster.jpg",
                new DateOnly(2026, 9, 20),
                true,
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                1,
                2,
                "Next")
        };

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)])
            ]),
            discoveryService: new FakeDiscoveryService(),
            comingUpService: new FakeHomeComingUpService(comingUpItems));

        var result = await service.GetHomePersonalizedAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(result.IsPersonalized);
        Assert.Equal(
            [HomeSectionType.RecommendedForYou, HomeSectionType.ComingUp],
            result.Sections.Select(section => section.Type).ToList());
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.HotThisWeek);
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncReturnsFalseForColdUser()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("popular", "Popular", []),
                new RecommendationSection("trending", "Trending", []),
                new RecommendationSection("top-rated", "Top Rated", [])
            ]),
            discoveryService: new FakeDiscoveryService());

        var result = await service.GetHomePersonalizedAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.False(result.IsPersonalized);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.RecommendedForYou);
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncDedupesRecommendedAgainstHotThisWeek()
    {
        var heroItemId = Guid.Parse("11111111-1111-1111-1111-000000000001");
        var recommendedItems = Enumerable.Range(1, 10)
            .Select(seed => CreateRecommendationItem("movie", seed))
            .ToList();

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You", recommendedItems)
            ]),
            weeklySnapshotItems:
            [
                new SearchItem(
                    heroItemId,
                    "movie",
                    "Hero Title",
                    null,
                    null,
                    null,
                    null,
                    new DateOnly(2025, 1, 1),
                    9m,
                    1000,
                    2025)
            ],
            options: new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                HeroSectionSize = 5
            });

        var result = await service.GetHomePersonalizedAsync(new HomeCriteria(SearchContentType.All, 10), ContentLocaleResolver.EnglishUnitedStates);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Equal(10, recommended.Items.Count);
        Assert.Contains(recommended.Items, item => item.Id == recommendedItems[0].Id);
    }

    [Theory]
    [InlineData(10, 80)]
    [InlineData(20, 80)]
    public async Task GetHomePersonalizedAsyncUsesHomeScoredPoolCapForRecommendationDepth(
        int sectionSize,
        int expectedDepth)
    {
        var recommendations = new FakeRecommendationService(
        [
            new RecommendationSection(
                "recommended-for-you",
                "Recommended For You",
                Enumerable.Range(1, sectionSize).Select(seed => CreateRecommendationItem("movie", seed)).ToList())
        ]);
        var service = CreateService(
            recommendationService: recommendations,
            options: new HomeOptions
            {
                DefaultSectionSize = sectionSize,
                MaximumSectionSize = 20,
                HeroSectionSize = 10
            });

        await service.GetHomePersonalizedAsync(
            new HomeCriteria(SearchContentType.All, sectionSize),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(expectedDepth, recommendations.LastSectionItemCount);
        Assert.False(recommendations.LastDiversify);
    }

    [Fact]
    public async Task HomeRequestsScoredPoolBelowSqlCandidateBudget()
    {
        const int sectionSize = 10;
        const int heroSectionSize = 10;
        var recommendationOptions = new RecommendationOptions();
        var heroWindow = sectionSize
            + heroSectionSize
            + recommendationOptions.HomeRecommendationSurplus;
        var homeOptions = new HomeOptions
        {
            DefaultSectionSize = sectionSize,
            MaximumSectionSize = 20,
            HeroSectionSize = heroSectionSize
        };
        var criteria = new HomeCriteria(SearchContentType.All, sectionSize);
        var sections = new RecommendationSection[]
        {
            new(
                "recommended-for-you",
                "Recommended For You",
                Enumerable.Range(1, sectionSize).Select(seed => CreateRecommendationItem("movie", seed)).ToList())
        };

        var personalizedRecommendations = new FakeRecommendationService(sections);
        var personalized = CreateService(recommendationService: personalizedRecommendations, options: homeOptions);
        await personalized.GetHomePersonalizedAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        var homeRecommendations = new FakeRecommendationService(sections);
        var home = CreateService(recommendationService: homeRecommendations, options: homeOptions);
        await home.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        const int largestSectionSize = 20;
        var largestRail = new FakeRecommendationService(sections);
        var largestHome = CreateService(recommendationService: largestRail, options: homeOptions);
        await largestHome.GetHomePersonalizedAsync(
            new HomeCriteria(SearchContentType.All, largestSectionSize),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(500, recommendationOptions.MaximumCandidates);
        Assert.NotNull(personalizedRecommendations.LastSectionItemCount);
        Assert.Equal(80, personalizedRecommendations.LastSectionItemCount);
        Assert.InRange(personalizedRecommendations.LastSectionItemCount.Value, heroWindow, 80);
        Assert.NotEqual(recommendationOptions.MaximumCandidates, personalizedRecommendations.LastSectionItemCount);
        Assert.Equal(personalizedRecommendations.LastSectionItemCount, homeRecommendations.LastSectionItemCount);
        Assert.Equal(80, largestRail.LastSectionItemCount);
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncRequestsScoredPoolAndRequestedType()
    {
        var recommendations = new FakeRecommendationService(
        [
            new RecommendationSection(
                "recommended-for-you",
                "Recommended For You",
                Enumerable.Range(1, 10).Select(seed => CreateRecommendationItem("tv", seed)).ToList())
        ]);
        var service = CreateService(
            recommendationService: recommendations,
            options: new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                HeroSectionSize = 10
            });

        var result = await service.GetHomePersonalizedAsync(
            new HomeCriteria(SearchContentType.Tv, 10),
            ContentLocaleResolver.EnglishUnitedStates);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Equal(RecommendationContentType.Tv, recommendations.LastContentType);
        Assert.False(recommendations.LastDiversify);
        Assert.Equal(80, recommendations.LastSectionItemCount);
        Assert.Equal(10, recommended.Items.Count);
        Assert.All(recommended.Items, item => Assert.Equal("tv", item.ContentType));
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncRemovesHeroOverlapWhenSurplusExists()
    {
        var heroId = Guid.Parse("11111111-1111-1111-1111-000000000001");
        var recommendedItems = Enumerable.Range(1, 11)
            .Select(seed => CreateRecommendationItem("movie", seed))
            .ToList();
        recommendedItems[0] = recommendedItems[0] with { Id = heroId };

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You", recommendedItems)
            ]),
            options: new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                HeroSectionSize = 5
            });

        var result = await service.GetHomePersonalizedAsync(
            new HomeCriteria(SearchContentType.All, 10),
            ContentLocaleResolver.EnglishUnitedStates);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Equal(10, recommended.Items.Count);
        Assert.DoesNotContain(recommended.Items, item => item.Id == heroId);
        Assert.Contains(recommended.Items, item => item.Id == recommendedItems[1].Id);
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncCapsGenreAfterRemovingHeroOverlap()
    {
        var heroId = Guid.Parse("11111111-1111-1111-1111-000000000001");
        var genreId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recommendedItems = Enumerable.Range(1, 12)
            .Select(seed => CreateRecommendationItem("movie", seed) with
            {
                DiversityGenreIds = [genreId]
            })
            .ToList();
        recommendedItems[0] = recommendedItems[0] with { Id = heroId };

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You", recommendedItems)
            ]),
            options: new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                HeroSectionSize = 5
            });

        var genreCap = new RecommendationOptions().DiversityMaxPerGenre;

        var result = await service.GetHomePersonalizedAsync(
            new HomeCriteria(SearchContentType.All, 10),
            ContentLocaleResolver.EnglishUnitedStates);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Equal(genreCap, recommended.Items.Count);
        Assert.DoesNotContain(recommended.Items, item => item.Id == heroId);
        Assert.Equal(recommendedItems[1].Id, recommended.Items[0].Id);
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncSeparatesAdjacentPrimaryGenresFromTheScoredPool()
    {
        var actionGenreId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var comedyGenreId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var recommendedItems = new List<RecommendationItem>
        {
            CreateRecommendationItem("movie", 1) with { DiversityGenreIds = [actionGenreId], Score = 0.90m },
            CreateRecommendationItem("movie", 2) with { DiversityGenreIds = [actionGenreId], Score = 0.80m },
            CreateRecommendationItem("movie", 3) with { DiversityGenreIds = [comedyGenreId], Score = 0.70m }
        };

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You", recommendedItems)
            ]));

        var result = await service.GetHomePersonalizedAsync(
            new HomeCriteria(SearchContentType.All, 10),
            ContentLocaleResolver.EnglishUnitedStates);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Equal(
            [recommendedItems[0].Id, recommendedItems[2].Id, recommendedItems[1].Id],
            recommended.Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncBackfillsFromLowerScoredPoolItemsOnly()
    {
        const int collectionId = 77;
        var recommendedItems = new List<RecommendationItem>();
        for (var seed = 1; seed <= 6; seed++)
        {
            recommendedItems.Add(CreateRecommendationItem("movie", seed) with
            {
                Score = 0.95m - (seed * 0.01m),
                TmdbCollectionId = collectionId,
                DiversityGenreIds = [Guid.NewGuid()]
            });
        }

        for (var seed = 7; seed <= 16; seed++)
        {
            recommendedItems.Add(CreateRecommendationItem("movie", seed) with
            {
                Score = 0.40m - (seed * 0.01m),
                DiversityGenreIds = [Guid.NewGuid()]
            });
        }

        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You", recommendedItems)
            ]),
            discoveryService: new FakeDiscoveryService());

        var result = await service.GetHomePersonalizedAsync(
            new HomeCriteria(SearchContentType.All, 10),
            ContentLocaleResolver.EnglishUnitedStates);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);
        var collectionIds = recommendedItems.Take(6).Select(item => item.Id).ToHashSet();

        Assert.Equal(10, recommended.Items.Count);
        Assert.Equal(1, recommended.Items.Count(item => collectionIds.Contains(item.Id)));
        Assert.Contains(recommended.Items, item => !collectionIds.Contains(item.Id));
        Assert.All(recommended.Items, item => Assert.Contains(recommendedItems, source => source.Id == item.Id));
    }

    [Fact]
    public async Task GetHomePersonalizedAsyncWorksWithoutBrowseFirst()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)])
            ]),
            discoveryService: new FakeDiscoveryService());

        var result = await service.GetHomePersonalizedAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(result.IsPersonalized);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.RecommendedForYou);
    }

    [Fact]
    public async Task GetHomeBrowseAsyncRespectsMovieTypeFilter()
    {
        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: new FakeDiscoveryService());

        var result = await service.GetHomeBrowseAsync(new HomeCriteria(SearchContentType.Movie, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.All(
            result.Sections.SelectMany(section => section.Items),
            item => Assert.Equal("movie", item.ContentType));
    }

    [Fact]
    public async Task GetHomeBrowseAsyncDoesNotUseAggregateHomeCache()
    {
        var cachedResult = new HomeResult(
            [new HomeSection(HomeSectionType.Popular, "Popular", [CreateHomeItem("movie", 1)], 1)],
            false);

        var cache = new FakeCacheService(cachedResult);
        var service = CreateService(
            cache: cache,
            recommendationService: new FakeRecommendationService([]),
            discoveryService: new FakeDiscoveryService());

        var result = await service.GetHomeBrowseAsync(new HomeCriteria(SearchContentType.All, 5), ContentLocaleResolver.EnglishUnitedStates);

        Assert.False(cache.WasWritten);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.HotThisWeek);
    }

    private static HomeService CreateService(
        ICacheService? cache = null,
        IRecommendationService? recommendationService = null,
        IDiscoveryService? discoveryService = null,
        IWatchHistoryService? watchHistoryService = null,
        IReadOnlyList<SearchItem>? weeklySnapshotItems = null,
        IGetHomeComingUpService? comingUpService = null,
        HomeOptions? options = null,
        Guid? userId = null,
        ITrendingWeekListService? trendingWeekListService = null)
    {
        var homeOptions = options ?? new HomeOptions
        {
            DefaultSectionSize = 10,
            MaximumSectionSize = 20,
            GenreSections = ["Science Fiction"]
        };

        return new HomeService(
            new FakeCurrentUser(userId ?? UserId),
            CreateScopeFactory(
                recommendationService,
                discoveryService,
                watchHistoryService,
                weeklySnapshotItems,
                comingUpService,
                cache,
                homeOptions,
                trendingWeekListService),
            cache ?? new FakeCacheService(),
            Options.Create(homeOptions),
            Options.Create(new ReleaseRegionOptions()),
            Options.Create(new RecommendationOptions()),
            NullLogger<HomeService>.Instance);
    }

    private static IServiceScopeFactory CreateScopeFactory(
        IRecommendationService? recommendationService = null,
        IDiscoveryService? discoveryService = null,
        IWatchHistoryService? watchHistoryService = null,
        IReadOnlyList<SearchItem>? weeklySnapshotItems = null,
        IGetHomeComingUpService? comingUpService = null,
        ICacheService? sharedCache = null,
        HomeOptions? options = null,
        ITrendingWeekListService? trendingWeekListService = null)
    {
        var homeOptions = options ?? new HomeOptions
        {
            DefaultSectionSize = 10,
            MaximumSectionSize = 20,
            GenreSections = ["Science Fiction"]
        };
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(homeOptions));
        services.AddScoped<ICurrentUser>(_ => new FakeCurrentUser(UserId));
        services.AddScoped<IRecommendationService>(_ =>
            recommendationService ?? new FakeRecommendationService([]));
        services.AddScoped<IDiscoveryService>(_ =>
            discoveryService ?? new FakeDiscoveryService());
        services.AddScoped<IWatchHistoryService>(_ =>
            watchHistoryService ?? new FakeWatchHistoryService([]));
        var snapshotItems = weeklySnapshotItems ?? CreateWeeklySnapshotItems(25);
        services.AddScoped<IHotThisWeekTrendingSnapshotService>(_ =>
            new FakeWeeklyTrendingSnapshotService(snapshotItems));
        services.AddScoped<ISummaryLocalizationOverlayService>(_ =>
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());
        services.AddLogging();
        services.AddSingleton<HotThisWeekLoadCoordinator>();
        services.AddScoped<ITrendingWeekListService>(_ =>
            trendingWeekListService ?? new FakeTrendingWeekListService(CreateFallbackWeekItems()));
        services.AddScoped<IOnTvThisWeekService, FakeOnTvThisWeekService>();
        services.AddScoped<INowInTheatersService, FakeNowInTheatersService>();
        services.AddScoped<IHotThisWeekService, HotThisWeekService>();
        services.AddScoped<IHomeWeeklyTrendingSectionService, HomeWeeklyTrendingSectionService>();
        services.AddSingleton(Options.Create(new TopRatedOptions()));
        services.AddScoped<IGenreReadRepository>(_ => new PassthroughGenreReadRepository());
        services.AddScoped<ISearchRepository>(_ => new PassthroughSearchRepository());
        services.AddScoped<IHomeTopRatedService, HomeTopRatedService>();
        services.AddScoped<IGetHomeComingUpService>(_ =>
            comingUpService ?? new FakeHomeComingUpService());
        services.AddSingleton<ICacheService>(_ => sharedCache ?? new PassthroughCacheService());
        services.AddSingleton<ISearchRefreshLockService, TestSearchRefreshLockService>();
        services.AddScoped<IHomeGlobalSectionsProvider, HomeGlobalSectionsProvider>();

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static List<SearchItem> CreateWeeklySnapshotItems(
        int count,
        Guid? firstItemId = null,
        string? firstItemTitle = null)
    {
        return Enumerable.Range(1, count)
            .Select(index =>
            {
                var type = index % 2 == 0 ? "tv" : "movie";
                var id = index == 1 && firstItemId.HasValue
                    ? firstItemId.Value
                    : Guid.Parse($"11111111-1111-1111-1111-{index:D12}");
                var title = index == 1 && firstItemTitle is not null
                    ? firstItemTitle
                    : $"Weekly Snapshot {index}";
                return new SearchItem(
                    id,
                    type,
                    title,
                    null,
                    null,
                    null,
                    null,
                    new DateOnly(2025, 1, 1),
                    8m,
                    1000,
                    2025);
            })
            .ToList();
    }

    private static List<SearchItem> CreateFallbackWeekItems() =>
    [
        new SearchItem(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-000000000020"),
            "tv",
            "Week Fallback",
            null,
            null,
            "/poster.jpg",
            null,
            new DateOnly(2021, 1, 1),
            7m,
            2000,
            2021)
    ];

    private sealed class FakeTrendingWeekListService(IReadOnlyList<SearchItem> items) : ITrendingWeekListService
    {
        public int CallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPageAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var page = items.Take(criteria.PageSize).ToList();
            return Task.FromResult(new PaginatedResult<SearchItem>(
                page,
                criteria.Page,
                criteria.PageSize,
                page.Count,
                page.Count == 0 ? 0 : 1));
        }
    }

    private sealed class FakeOnTvThisWeekService : IOnTvThisWeekService
    {
        public Task<PaginatedResult<SearchItem>> GetOnTvThisWeekAsync(
            OnTvThisWeekCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            var item = new SearchItem(
                Guid.Parse("abababab-abab-abab-abab-abababababab"),
                "tv",
                "On Air Drama",
                null,
                null,
                "/poster.jpg",
                null,
                new DateOnly(2024, 1, 1),
                8m,
                80,
                2024);
            return Task.FromResult(new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1));
        }
    }

    private sealed class FakeNowInTheatersService : INowInTheatersService
    {
        public Task<PaginatedResult<SearchItem>> GetNowInTheatersAsync(
            NowInTheatersCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            var item = new SearchItem(
                Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd"),
                "movie",
                "Cinema One",
                null,
                null,
                "/poster.jpg",
                null,
                new DateOnly(2026, 1, 1),
                7.5m,
                120,
                2026);
            return Task.FromResult(new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1));
        }
    }

    private static HomeItem CreateHomeItem(string type, int seed) =>
        new(
            Guid.Parse($"cccccccc-cccc-cccc-cccc-{seed:D012}"),
            type,
            $"Title {seed}",
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            8m,
            100);

    private static RecommendationItem CreateRecommendationItem(string type, int seed) =>
        new(
            Guid.Parse($"dddddddd-dddd-dddd-dddd-{seed:D012}"),
            type,
            $"Title {seed}",
            null,
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            8m,
            100,
            2020,
            0.9m,
            "Because you watched");

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeCacheService(HomeResult? cachedResult = null) : ICacheService
    {
        public bool WasRead { get; private set; }

        public bool WasWritten { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            WasRead = true;

            if (cachedResult is not null && typeof(T) == typeof(HomeCacheEntry))
            {
                return Task.FromResult<T?>((T)(object)new HomeCacheEntry { Result = cachedResult });
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            if (typeof(T) == typeof(HomeCacheEntry))
            {
                WasWritten = true;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeRecommendationService(IReadOnlyList<RecommendationSection> sections) : IRecommendationService
    {
        public Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(Guid movieId, SimilarContentCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(Guid tvShowId, SimilarContentCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(RecommendationCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public RecommendationContentType? LastContentType { get; private set; }

        public int? LastSectionItemCount { get; private set; }

        public bool LastDiversify { get; private set; } = true;

        public bool LastIncludeBecauseYouWatched { get; private set; } = true;

        public Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
            bool includeColdStartDiscoverySections = true,
            string contentLocale = "en-US",
            RecommendationContentType contentType = RecommendationContentType.All,
            int? sectionItemCount = null,
            bool diversify = true,
            bool includeBecauseYouWatched = true,
            CancellationToken cancellationToken = default)
        {
            LastContentType = contentType;
            LastSectionItemCount = sectionItemCount;
            LastDiversify = diversify;
            LastIncludeBecauseYouWatched = includeBecauseYouWatched;
            return Task.FromResult(sections);
        }
    }

    private sealed class CancellingRecommendationService : IRecommendationService
    {
        public Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(Guid movieId, SimilarContentCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(Guid tvShowId, SimilarContentCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(RecommendationCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
            bool includeColdStartDiscoverySections = true,
            string contentLocale = "en-US",
            RecommendationContentType contentType = RecommendationContentType.All,
            int? sectionItemCount = null,
            bool diversify = true,
            bool includeBecauseYouWatched = true,
            CancellationToken cancellationToken = default) =>
            Task.FromCanceled<IReadOnlyList<RecommendationSection>>(cancellationToken);
    }

    private sealed class SharedHomeCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public int HomeSetCount { get; private set; }

        public int GlobalSetCount { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            _entries.TryGetValue(key, out var value);
            return Task.FromResult(value as T);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value!;

            if (key.StartsWith(HomeGlobalCacheKeys.Prefix, StringComparison.Ordinal))
            {
                GlobalSetCount++;
            }
            else if (key.StartsWith(HomeCacheKeys.Prefix, StringComparison.Ordinal))
            {
                HomeSetCount++;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class PassthroughCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestSearchRefreshLockService : ISearchRefreshLockService
    {
        private readonly LocalSearchRefreshSingleFlightGate _localGate = new();

        public Task<SearchRefreshLockHandle?> TryAcquireAsync(
            string lockKey,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            if (_localGate.TryAcquire(lockKey, out var lockToken))
            {
                return Task.FromResult<SearchRefreshLockHandle?>(
                    new SearchRefreshLockHandle(lockKey, lockToken, SearchRefreshLockBackend.LocalSingleFlight));
            }

            return Task.FromResult<SearchRefreshLockHandle?>(null);
        }

        public Task ReleaseAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            CancellationToken cancellationToken = default)
        {
            _localGate.Release(lockKey, lockToken);
            return Task.CompletedTask;
        }

        public Task<bool> TryRenewAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class CountingDiscoveryService : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public int PopularCallCount { get; private set; }

        public int NewReleasesCallCount { get; private set; }

        public int TopRatedCallCount { get; private set; }

        public int GenreCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            PopularCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 10));
        }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            return Task.FromResult(CreateResult(criteria, "tv", 20));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            NewReleasesCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 30));
        }

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TopRatedCallCount++;
            return Task.FromResult(CreateResult(criteria, "tv", 40));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            GenreCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 50));
        }

        private static PaginatedResult<SearchItem> CreateResult(
            DiscoveryCriteria criteria,
            string type,
            int seed)
        {
            var item = new SearchItem(
                Guid.Parse($"eeeeeeee-eeee-eeee-eeee-{seed:D012}"),
                type,
                $"Discovery {seed}",
                null,
                null,
                null,
                null,
                new DateOnly(2021, 1, 1),
                7m,
                2000,
                2021);

            return new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1);
        }
    }

    private sealed class EmptyTrendingDiscoveryService : FakeDiscoveryService
    {
        public override Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, criteria.PageSize, 0, 0));
    }

    private class FakeDiscoveryService(bool includeGenre = true) : IDiscoveryService
    {
        public virtual Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "movie", 10));

        public virtual Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "tv", 20));

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "movie", 30));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "tv", 40));

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            Task.FromResult(includeGenre
                ? CreateResult(criteria, "movie", 50)
                : new PaginatedResult<SearchItem>([], 1, criteria.PageSize, 0, 0));

        private static PaginatedResult<SearchItem> CreateResult(
            DiscoveryCriteria criteria,
            string type,
            int seed)
        {
            var item = new SearchItem(
                Guid.Parse($"eeeeeeee-eeee-eeee-eeee-{seed:D012}"),
                type,
                $"Discovery {seed}",
                null,
                null,
                null,
                null,
                new DateOnly(2021, 1, 1),
                7m,
                2000,
                2021);

            return new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1);
        }
    }

    private sealed class FakeWatchHistoryService(IReadOnlyList<ContinueWatchingItemResult> items) : IWatchHistoryService
    {
        public Task<WatchMutationResult> MarkMovieWatchedAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UnmarkMovieWatchedAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieWatchStatusResult> GetMovieWatchStatusAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<WatchedMovieResult>> GetWatchedMoviesAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WatchMutationResult> MarkEpisodeWatchedAsync(Guid episodeId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UnmarkEpisodeWatchedAsync(Guid episodeId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeWatchStatusResult> GetEpisodeWatchStatusAsync(Guid episodeId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<WatchedEpisodeResult>> GetWatchedEpisodesAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecentWatchHistoryItemResult>> GetRecentWatchHistoryAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowWatchProgressResult> GetTvShowWatchProgressAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonWatchProgressResult> GetSeasonWatchProgressAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ContinueWatchingItemResult>> GetContinueWatchingAsync(
            int sectionSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContinueWatchingItemResult>>(items.Take(sectionSize).ToList());

        public Task<SeasonWatchedEpisodesResult> GetSeasonWatchedEpisodesAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateEpisodeWatchStateAsync(
            Guid tvShowId,
            IReadOnlyList<Guid> episodeIds,
            bool watched,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MarkThroughEpisodeResult> MarkThroughEpisodeAsync(
            Guid tvShowId,
            Guid episodeId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateSeasonWatchStateAsync(
            Guid tvShowId,
            int seasonNumber,
            bool watched,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateTvShowWatchStateAsync(
            Guid tvShowId,
            bool watched,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class PassthroughGenreReadRepository : IGenreReadRepository
    {
        public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]);

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
            IReadOnlyList<Guid> genreIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
            IReadOnlyList<Guid> movieIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            MovieApp.UnitTests.Persistence.GenreReadRepositoryTestDefaults.EmptyMovieGenresAsync(
                movieIds,
                maxGenresPerItem,
                cancellationToken);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            MovieApp.UnitTests.Persistence.GenreReadRepositoryTestDefaults.EmptyTvGenresAsync(
                tvShowIds,
                maxGenresPerItem,
                cancellationToken);
    }

    private sealed class PassthroughSearchRepository : ISearchRepository
    {
        public Task<PaginatedResult<SearchItem>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(
            SearchContentType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(6.0m);

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
            IReadOnlyList<SearchItem> items,
            Guid genreId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<CatalogContentKey>>(new HashSet<CatalogContentKey>());

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(
            IReadOnlyList<SearchItem> items,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<CatalogContentKey>>(
                items.Select(item => new CatalogContentKey(item.Id, item.Type)).ToHashSet());

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHomeComingUpService(
        IReadOnlyList<Application.Models.CatalogFollows.CatalogUpcomingItemResult>? items = null)
        : IGetHomeComingUpService
    {
        public Task<IReadOnlyList<Application.Models.CatalogFollows.CatalogUpcomingItemResult>> GetItemsAsync(
            int limit,
            string? releaseRegion,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Application.Models.CatalogFollows.CatalogUpcomingItemResult>>(
                (items ?? []).Take(limit).ToList());
    }

    private sealed class FakeWeeklyTrendingSnapshotService(IReadOnlyList<SearchItem> items) : IHotThisWeekTrendingSnapshotService
    {
        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (items.Count == 0)
            {
                return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(null);
            }

            return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(new HotThisWeekTrendingSnapshotEntry
            {
                RefreshedAt = DateTimeOffset.UtcNow,
                Items = items,
            });
        }

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
