using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Home;

public sealed class HomeServiceStampedeTests
{
    private static readonly Guid UserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task ConcurrentMissesForSameHomeCacheKeyBuildOnce()
    {
        var cache = new SharedHomeCacheService();
        var recommendation = new CountingRecommendationService(TimeSpan.FromMilliseconds(300));
        var service = CreateService(cache, recommendation);
        var criteria = new HomeCriteria(SearchContentType.All, 10);
        var counters = HomeStampedePerfAmbient.BeginScenario("concurrent-same-key");

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        HomeStampedePerfAmbient.EndScenario();

        Assert.Equal(10, results.Length);
        Assert.All(results, result => Assert.NotEmpty(result.Sections));
        Assert.Equal(1, counters.HomeBuildCompletions);
        Assert.Equal(1, recommendation.HomeCallCount);
        Assert.Equal(1, cache.HomeSetCount);
    }

    [Fact]
    public async Task WarmHomeCacheDoesNotRebuildUnderConcurrency()
    {
        var cache = new SharedHomeCacheService();
        var recommendation = new CountingRecommendationService(TimeSpan.Zero);
        var service = CreateService(cache, recommendation);
        var criteria = new HomeCriteria(SearchContentType.All, 10);

        _ = await service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);
        recommendation.ResetCount();

        var counters = HomeStampedePerfAmbient.BeginScenario("warm-concurrent");
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates))
            .ToArray();
        await Task.WhenAll(tasks);
        HomeStampedePerfAmbient.EndScenario();

        Assert.Equal(0, counters.HomeBuildCompletions);
        Assert.Equal(0, recommendation.HomeCallCount);
        Assert.Equal(1, cache.HomeSetCount);
    }

    [Fact]
    public async Task ConcurrentMissesForDifferentHomeCacheKeysBuildIndependently()
    {
        var cache = new SharedHomeCacheService();
        var recommendation = new CountingRecommendationService(TimeSpan.FromMilliseconds(150));
        var serviceTr = CreateService(cache, recommendation, UserId);
        var serviceUs = CreateService(cache, recommendation, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var criteria = new HomeCriteria(SearchContentType.All, 10);
        var counters = HomeStampedePerfAmbient.BeginScenario("different-keys");

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => serviceTr.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates, "TR"))
            .Concat(Enumerable.Range(0, 5)
                .Select(_ => serviceUs.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates, "US")))
            .ToArray();
        await Task.WhenAll(tasks);

        HomeStampedePerfAmbient.EndScenario();

        Assert.Equal(2, counters.HomeBuildCompletions);
        Assert.Equal(2, recommendation.HomeCallCount);
        Assert.Equal(2, cache.HomeSetCount);
    }

    [Fact]
    public async Task FailedHomeBuildClearsInflightAndAllowsRetry()
    {
        var cache = new SharedHomeCacheService();
        var recommendation = new FlakyRecommendationService();
        var coordinator = new HomeLoadCoordinator();
        var service = CreateService(cache, recommendation, UserId, coordinator);
        var criteria = new HomeCriteria(SearchContentType.All, 10);
        var cacheKey = await ResolveCacheKeyAsync(cache, UserId, criteria, ContentLocaleResolver.EnglishUnitedStates);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates));

        Assert.Null(coordinator.TryGetInFlight(cacheKey));

        var counters = HomeStampedePerfAmbient.BeginScenario("retry-after-failure");
        var result = await service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);
        HomeStampedePerfAmbient.EndScenario();

        Assert.NotEmpty(result.Sections);
        Assert.Equal(1, counters.HomeBuildCompletions);
        Assert.Equal(2, recommendation.HomeCallCount);
    }

    [Fact]
    public async Task CompletedInflightEntryIsRemovedFromCoordinator()
    {
        var cache = new SharedHomeCacheService();
        var recommendation = new CountingRecommendationService(TimeSpan.FromMilliseconds(50));
        var coordinator = new HomeLoadCoordinator();
        var service = CreateService(cache, recommendation, UserId, coordinator);
        var criteria = new HomeCriteria(SearchContentType.All, 10);
        var cacheKey = await ResolveCacheKeyAsync(cache, UserId, criteria, ContentLocaleResolver.EnglishUnitedStates);

        await service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Null(coordinator.TryGetInFlight(cacheKey));
    }

    [Fact]
    public async Task WaiterCancellationDoesNotCancelSharedHomeBuild()
    {
        var cache = new SharedHomeCacheService();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recommendation = new GatedRecommendationService(started, release);
        var service = CreateService(cache, recommendation);
        var criteria = new HomeCriteria(SearchContentType.All, 10);

        var owner = service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);
        await started.Task;

        using var waiterCts = new CancellationTokenSource();
        var waiter = service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates, cancellationToken: waiterCts.Token);
        await Task.Delay(50);
        waiterCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        release.TrySetResult();

        var result = await owner;
        Assert.NotEmpty(result.Sections);
        Assert.Equal(1, recommendation.HomeCallCount);
    }

    [Fact]
    public async Task LockWaiterRechecksCacheAfterOwnerPopulatesIt()
    {
        var cache = new SharedHomeCacheService();
        var recommendation = new CountingRecommendationService(TimeSpan.FromMilliseconds(250));
        var service = CreateService(cache, recommendation);
        var criteria = new HomeCriteria(SearchContentType.All, 10);

        await Task.WhenAll(
            service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates),
            service.GetHomeAsync(criteria, ContentLocaleResolver.EnglishUnitedStates));

        Assert.Equal(1, recommendation.HomeCallCount);
        Assert.Equal(1, cache.HomeSetCount);
    }

    private static HomeService CreateService(
        ICacheService cache,
        IRecommendationService recommendation,
        Guid? userId = null,
        HomeLoadCoordinator? coordinator = null,
        ISearchRefreshLockService? lockService = null)
    {
        var resolvedUserId = userId ?? UserId;
        var homeOptions = new HomeOptions
        {
            DefaultSectionSize = 10,
            MaximumSectionSize = 20,
            GenreSections = ["Science Fiction"]
        };

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(homeOptions));
        services.AddSingleton(Options.Create(new ReleaseRegionOptions()));
        services.AddSingleton(Options.Create(new RecommendationOptions()));
        services.AddScoped<ICurrentUser>(_ => new FakeCurrentUser(resolvedUserId));
        services.AddScoped<IRecommendationService>(_ => recommendation);
        services.AddScoped<IDiscoveryService, StubDiscoveryService>();
        services.AddScoped<IHotThisWeekTrendingSnapshotService>(_ => new StubSnapshotService());
        services.AddScoped<ISummaryLocalizationOverlayService>(_ =>
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());
        services.AddLogging();
        services.AddSingleton<HotThisWeekLoadCoordinator>();
        services.AddScoped<ITrendingWeekListService, StubTrendingWeekListService>();
        services.AddScoped<IOnTvThisWeekService, StubOnTvService>();
        services.AddScoped<INowInTheatersService, StubNowInTheatersService>();
        services.AddScoped<IHotThisWeekService, HotThisWeekService>();
        services.AddScoped<IHomeWeeklyTrendingSectionService, HomeWeeklyTrendingSectionService>();
        services.AddSingleton(Options.Create(new TopRatedOptions()));
        services.AddScoped<IGenreReadRepository, StubGenreReadRepository>();
        services.AddScoped<ISearchRepository, StubSearchRepository>();
        services.AddScoped<IHomeTopRatedService, HomeTopRatedService>();
        services.AddScoped<IGetHomeComingUpService, StubComingUpService>();
        services.AddSingleton(cache);

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new HomeService(
            new FakeCurrentUser(resolvedUserId),
            scopeFactory,
            cache,
            lockService ?? new SearchTestDoubles.InMemorySearchRefreshLockService(),
            coordinator ?? new HomeLoadCoordinator(),
            Options.Create(homeOptions),
            Options.Create(new ReleaseRegionOptions()),
            Options.Create(new RecommendationOptions()),
            NullLogger<HomeService>.Instance);
    }

    private static async Task<string> ResolveCacheKeyAsync(
        ICacheService cache,
        Guid userId,
        HomeCriteria criteria,
        string contentLocale)
    {
        var generation = await new UserRecommendationCacheGeneration(cache).GetAsync(userId);
        return HomeCacheKeys.Create(
            userId,
            criteria.Type,
            criteria.SectionSize,
            contentLocale,
            "TR",
            generation);
    }

    private sealed class SharedHomeCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);
        private int _homeSetCount;

        public int HomeSetCount => _homeSetCount;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value!;
            if (key.StartsWith(HomeCacheKeys.Prefix, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _homeSetCount);
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated => true;
    }

    private sealed class CountingRecommendationService(TimeSpan delay) : IRecommendationService
    {
        private int _homeCallCount;

        public int HomeCallCount => _homeCallCount;

        public void ResetCount() => _homeCallCount = 0;

        public Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
            Guid movieId,
            SimilarContentCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
            Guid tvShowId,
            SimilarContentCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
            RecommendationCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
            bool includeColdStartDiscoverySections = true,
            string contentLocale = ContentLocaleResolver.EnglishUnitedStates,
            RecommendationContentType contentType = RecommendationContentType.All,
            int? sectionItemCount = null,
            bool diversify = true,
            bool includeBecauseYouWatched = true,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _homeCallCount);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return [];
        }
    }

    private sealed class FlakyRecommendationService : IRecommendationService
    {
        private int _homeCallCount;

        public int HomeCallCount => _homeCallCount;

        public Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
            Guid movieId,
            SimilarContentCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
            Guid tvShowId,
            SimilarContentCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
            RecommendationCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
            bool includeColdStartDiscoverySections = true,
            string contentLocale = ContentLocaleResolver.EnglishUnitedStates,
            RecommendationContentType contentType = RecommendationContentType.All,
            int? sectionItemCount = null,
            bool diversify = true,
            bool includeBecauseYouWatched = true,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _homeCallCount);
            if (_homeCallCount == 1)
            {
                throw new InvalidOperationException("simulated home build failure");
            }

            return Task.FromResult<IReadOnlyList<RecommendationSection>>([]);
        }
    }

    private sealed class GatedRecommendationService(
        TaskCompletionSource started,
        TaskCompletionSource release) : IRecommendationService
    {
        private int _homeCallCount;

        public int HomeCallCount => _homeCallCount;

        public Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
            Guid movieId,
            SimilarContentCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
            Guid tvShowId,
            SimilarContentCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
            RecommendationCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
            bool includeColdStartDiscoverySections = true,
            string contentLocale = ContentLocaleResolver.EnglishUnitedStates,
            RecommendationContentType contentType = RecommendationContentType.All,
            int? sectionItemCount = null,
            bool diversify = true,
            bool includeBecauseYouWatched = true,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _homeCallCount);
            started.TrySetResult();
            await release.Task;
            return [];
        }
    }

    private sealed class StubDiscoveryService : IDiscoveryService
    {
        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Empty(criteria);

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Empty(criteria);

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Empty(criteria);

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Empty(criteria);

        public Task<PaginatedResult<SearchItem>> GetHiddenGemsAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedBrowseAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Empty(criteria);

        private static Task<PaginatedResult<SearchItem>> Empty(DiscoveryCriteria criteria) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));
    }

    private sealed class StubSnapshotService : IHotThisWeekTrendingSnapshotService
    {
        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(new HotThisWeekTrendingSnapshotEntry
            {
                RefreshedAt = DateTimeOffset.UtcNow,
                Items =
                [
                    new SearchItem(
                        Guid.Parse("11111111-1111-1111-1111-000000000001"),
                        "movie",
                        "Weekly",
                        null,
                        null,
                        null,
                        null,
                        null,
                        8m,
                        10,
                        null)
                ]
            });

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubTrendingWeekListService : ITrendingWeekListService
    {
        public Task<PaginatedResult<SearchItem>> GetPageAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));
    }

    private sealed class StubOnTvService : IOnTvThisWeekService
    {
        public Task<PaginatedResult<SearchItem>> GetOnTvThisWeekAsync(
            OnTvThisWeekCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            var item = new SearchItem(
                Guid.Parse("abababab-abab-abab-abab-abababababab"),
                "tv",
                "On Air",
                null,
                null,
                null,
                null,
                null,
                8m,
                10,
                null);
            return Task.FromResult(new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1));
        }
    }

    private sealed class StubNowInTheatersService : INowInTheatersService
    {
        public Task<PaginatedResult<SearchItem>> GetNowInTheatersAsync(
            NowInTheatersCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            var item = new SearchItem(
                Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd"),
                "movie",
                "Cinema",
                null,
                null,
                null,
                null,
                null,
                7m,
                10,
                null);
            return Task.FromResult(new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1));
        }
    }

    private sealed class StubComingUpService : IGetHomeComingUpService
    {
        public Task<IReadOnlyList<Application.Models.CatalogFollows.CatalogUpcomingItemResult>> GetItemsAsync(
            int maxItems,
            string? releaseRegion,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Application.Models.CatalogFollows.CatalogUpcomingItemResult>>([]);
    }

    private sealed class StubGenreReadRepository : IGenreReadRepository
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

    private sealed class StubSearchRepository : ISearchRepository
    {
        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> SearchAsync(SearchCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(string query, int limit, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(SearchContentType type, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(IReadOnlyList<SearchItem> items, Guid genreId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(IReadOnlyList<SearchItem> items, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
