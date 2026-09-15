using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
using MovieApp.Infrastructure.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Services.WatchHistory;

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

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 10));

        Assert.True(cache.WasRead);
        Assert.False(cache.WasWritten);
        Assert.Equal(cachedResult, result);
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

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 2));

        Assert.False(result.IsPersonalized);
        Assert.Equal(
            [HomeSectionType.HotThisWeek, HomeSectionType.TopRated, HomeSectionType.NewReleases],
            result.Sections.Select(section => section.Type).ToList());
        Assert.True(cache.WasWritten);
    }

    [Fact]
    public async Task GetHomeAsyncBuildsPersonalizedSectionsInOrder()
    {
        var discovery = new CountingDiscoveryService();
        var service = CreateService(
            recommendationService: new FakeRecommendationService(
            [
                new RecommendationSection("recommended-for-you", "Recommended For You",
                    [CreateRecommendationItem("movie", 1)]),
                new RecommendationSection("because-you-watched", "Because You Watched",
                    [CreateRecommendationItem("tv", 2)]),
                new RecommendationSection("similar-to-favorites", "Based On Your Favorites",
                    [CreateRecommendationItem("movie", 3)])
            ]),
            discoveryService: discovery);

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5));

        Assert.True(result.IsPersonalized);
        Assert.Equal(
            [
                HomeSectionType.HotThisWeek,
                HomeSectionType.RecommendedForYou,
                HomeSectionType.TopRated,
                HomeSectionType.NewReleases
            ],
            result.Sections.Select(section => section.Type).ToList());
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Equal(0, discovery.PopularCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(0, discovery.GenreCallCount);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.BecauseYouWatched);
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

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5));

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

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.Movie, 5));

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

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5));
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Equal(2, recommended.Items.Count);
        Assert.Equal(2, recommended.Items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task GetHomeAsyncRejectsInvalidSectionSize()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 21)));
    }

    [Fact]
    public async Task GetHomeAsyncPropagatesCancellation()
    {
        var service = CreateService(
            recommendationService: new CancellingRecommendationService());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5), cts.Token));
    }

    [Fact]
    public async Task GetHomeAsyncColdStartLoadsTopRatedAndNewReleasesDiscovery()
    {
        var discovery = new CountingDiscoveryService();
        var service = CreateService(
            recommendationService: new FakeRecommendationService([]),
            discoveryService: discovery);

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5));

        Assert.False(result.IsPersonalized);
        Assert.Equal(
            [HomeSectionType.HotThisWeek, HomeSectionType.TopRated, HomeSectionType.NewReleases],
            result.Sections.Select(section => section.Type).ToList());
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Equal(0, discovery.PopularCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(0, discovery.GenreCallCount);
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
            hotThisWeekService: new FakeHotThisWeekService(
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
            ]),
            options: new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                HeroSectionSize = 5
            });

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 10));

        var hero = result.Sections.Single(section => section.Type == HomeSectionType.HotThisWeek);
        var recommended = result.Sections.Single(section => section.Type == HomeSectionType.RecommendedForYou);

        Assert.Single(hero.Items);
        Assert.Equal(10, recommended.Items.Count);
        Assert.Contains(recommended.Items, item => item.Id == recommendedItems[0].Id);
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
            hotThisWeekService: new FakeHotThisWeekService([]),
            discoveryService: new FakeDiscoveryService());

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5));

        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.HotThisWeek);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.RecommendedForYou);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.TopRated);
        Assert.Contains(result.Sections, section => section.Type == HomeSectionType.NewReleases);
    }

    private static HomeService CreateService(
        ICacheService? cache = null,
        IRecommendationService? recommendationService = null,
        IDiscoveryService? discoveryService = null,
        IWatchHistoryService? watchHistoryService = null,
        IHotThisWeekService? hotThisWeekService = null,
        HomeOptions? options = null,
        Guid? userId = null)
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
                hotThisWeekService,
                cache,
                homeOptions),
            cache ?? new FakeCacheService(),
            Options.Create(homeOptions));
    }

    private static IServiceScopeFactory CreateScopeFactory(
        IRecommendationService? recommendationService = null,
        IDiscoveryService? discoveryService = null,
        IWatchHistoryService? watchHistoryService = null,
        IHotThisWeekService? hotThisWeekService = null,
        ICacheService? sharedCache = null,
        HomeOptions? options = null)
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
        services.AddScoped<IHotThisWeekService>(_ =>
            hotThisWeekService ?? new FakeHotThisWeekService());
        services.AddSingleton<ICacheService>(_ => sharedCache ?? new PassthroughCacheService());
        services.AddSingleton<ISearchRefreshLockService, TestSearchRefreshLockService>();
        services.AddScoped<IHomeGlobalSectionsProvider, HomeGlobalSectionsProvider>();

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
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
            WasWritten = true;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeRecommendationService(IReadOnlyList<RecommendationSection> sections) : IRecommendationService
    {
        public Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
            Guid movieId,
            SimilarContentCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
            Guid tvShowId,
            SimilarContentCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
            RecommendationCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
            bool includeColdStartDiscoverySections = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(sections);
    }

    private sealed class CancellingRecommendationService : IRecommendationService
    {
        public Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
            Guid movieId,
            SimilarContentCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
            Guid tvShowId,
            SimilarContentCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
            RecommendationCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
            bool includeColdStartDiscoverySections = true,
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

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            PopularCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 10));
        }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            return Task.FromResult(CreateResult(criteria, "tv", 20));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            NewReleasesCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 30));
        }

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            TopRatedCallCount++;
            return Task.FromResult(CreateResult(criteria, "tv", 40));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
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
                50,
                2021);

            return new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1);
        }
    }

    private sealed class FakeDiscoveryService(bool includeGenre = true) : IDiscoveryService
    {
        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "movie", 10));

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "tv", 20));

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "movie", 30));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResult(criteria, "tv", 40));

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
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
                50,
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

    private sealed class FakeHotThisWeekService(IReadOnlyList<SearchItem>? items = null) : IHotThisWeekService
    {
        private readonly IReadOnlyList<SearchItem> _items = items ??
        [
            new SearchItem(
                Guid.Parse("ffffffff-ffff-ffff-ffff-000000000099"),
                "movie",
                "Hot This Week",
                null,
                null,
                null,
                null,
                new DateOnly(2025, 3, 1),
                8.8m,
                1200,
                2025)
        ];

        public Task<IReadOnlyList<SearchItem>> GetItemsAsync(
            SearchContentType type,
            int maxItems,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchItem>>(_items.Take(maxItems).ToList());
    }
}
