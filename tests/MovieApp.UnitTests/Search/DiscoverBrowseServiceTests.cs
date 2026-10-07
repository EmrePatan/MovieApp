using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Providers;
using MovieApp.UnitTests.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverBrowseServiceTests
{
    [Fact]
    public async Task BrowseAsyncReturnsCachedResultWithoutCallingProviders()
    {
        var cachedItem = CreateSearchItem("movie", Guid.NewGuid());
        var cache = new DiscoverBrowseFakeCacheService(
            new PaginatedResult<SearchItem>([cachedItem], 1, 20, 1, 1));
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.Equal(cachedItem.Id, result.Items[0].Id);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal(0, tvTracker.DiscoverTvShowsCallCount);
    }

    [Fact]
    public async Task BrowseAsyncUsesOnlyMovieProviderForMovieType()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEmpty(result.Items);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal(0, tvTracker.DiscoverTvShowsCallCount);
        Assert.All(result.Items, item => Assert.Equal("movie", item.Type));
    }

    [Fact]
    public async Task BrowseAsyncUsesBothProvidersForAllType()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.All), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEmpty(result.Items);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal(1, tvTracker.DiscoverTvShowsCallCount);
        Assert.Contains(result.Items, item => item.Type == "movie");
        Assert.Contains(result.Items, item => item.Type == "tv");
        Assert.True(result.TotalCount >= result.Items.Count);
    }

    [Fact]
    public async Task BrowseAsyncTrendingAllCompletesWithoutWalkingProviderPageCap()
    {
        const int providerPageSize = 20;
        const int providerTotalPages = 500;
        var movies = new CappedDiscoverMovieProvider(providerPageSize, providerTotalPages);
        var tvShows = new CappedDiscoverTvProvider(providerPageSize, providerTotalPages);
        var service = CreateService(movies, tvShows);

        var result = await service.BrowseAsync(
            CreateCriteria(SearchContentType.All, page: 1, pageSize: 5, sort: DiscoverBrowseSort.PopularityDesc),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(providerTotalPages * providerPageSize * 2, result.TotalCount);
        Assert.Equal(1, movies.DiscoverCallCount);
        Assert.Equal(1, tvShows.DiscoverCallCount);
    }

    [Fact]
    public async Task BrowseAsyncTrendingAllKeepsExhaustedSideTotalWhilePagingTheOther()
    {
        const int providerPageSize = 20;
        var movies = new CappedDiscoverMovieProvider(providerPageSize, totalPages: 1);
        var tvShows = new CappedDiscoverTvProvider(providerPageSize, totalPages: 500);
        var service = CreateService(movies, tvShows);

        var result = await service.BrowseAsync(
            CreateCriteria(SearchContentType.All, page: 2, pageSize: 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(20, result.Items.Count);
        Assert.Equal((1 * providerPageSize) + (500 * providerPageSize), result.TotalCount);
        Assert.Equal(1, movies.DiscoverCallCount);
        Assert.Equal(2, tvShows.DiscoverCallCount);
    }

    [Fact]
    public async Task BrowseAsyncMaterializesSummariesWithoutDetailCalls()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var movieRepository = new SummaryMovieRepository();
        var tvRepository = new SummaryTvShowRepository();
        var service = new DiscoverBrowseService(
            new DiscoveryServiceCallTracker(),
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(tvTracker),
            movieRepository,
            tvRepository,
            new FakeGenreReadRepository(),
            new FakeKeywordDiscoverReadRepository(),
            cache,
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoverBrowseService>.Instance,
            new SearchItemCatalogMetadataEnricher(new FakeGenreReadRepository()),
            SearchTestDoubles.CreateDisplayTitleEnricher(),
            new FakeTrendingWeekListService(),
            EmptyTrendingSnapshotService.Instance);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.All), ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(movieRepository.EnsureCount > 0);
        Assert.True(tvRepository.EnsureCount > 0);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
    }

    [Fact]
    public async Task BrowseAsyncThrowsWhenRequiredProviderFails()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker { FailDiscoverMovies = true };
        var service = CreateService(
            cache,
            movieTracker,
            new TvShowDataProviderCallTracker());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.BrowseAsync(CreateCriteria(SearchContentType.Movie), ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task BrowseAsyncNewReleasesWithoutFiltersUsesCatalogDiscoveryService()
    {
        var discoveryTracker = new DiscoveryServiceCallTracker();
        var movieTracker = new MovieDataProviderCallTracker();
        var service = CreateService(
            new DiscoverBrowseFakeCacheService(null),
            movieTracker,
            new TvShowDataProviderCallTracker(),
            discoveryTracker);

        var result = await service.BrowseAsync(CreateCriteria(
            SearchContentType.Movie,
            DiscoverBrowseMode.NewReleases), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, discoveryTracker.CatalogListNewReleasesCallCount);
        Assert.Equal(0, discoveryTracker.NewReleasesCallCount);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
        Assert.Single(result.Items);
        Assert.Equal("Title", result.Items[0].Title);
    }

    [Fact]
    public async Task BrowseAsyncTopRatedWithoutFiltersUsesCatalogDiscoveryService()
    {
        var discoveryTracker = new DiscoveryServiceCallTracker();
        var movieTracker = new MovieDataProviderCallTracker();
        var service = CreateService(
            new DiscoverBrowseFakeCacheService(null),
            movieTracker,
            new TvShowDataProviderCallTracker(),
            discoveryTracker);

        var result = await service.BrowseAsync(CreateCriteria(
            SearchContentType.All,
            DiscoverBrowseMode.TopRated), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, discoveryTracker.TopRatedCallCount);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task BrowseAsyncTopRatedWithFiltersStaysOnCatalogEngine()
    {
        var discoveryTracker = new DiscoveryServiceCallTracker();
        var movieTracker = new MovieDataProviderCallTracker();
        var service = CreateService(
            new DiscoverBrowseFakeCacheService(null),
            movieTracker,
            new TvShowDataProviderCallTracker(),
            discoveryTracker);

        await service.BrowseAsync(
            CreateCriteria(SearchContentType.Movie, DiscoverBrowseMode.TopRated) with
            {
                GenreIds = [Guid.NewGuid()]
            },
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, discoveryTracker.TopRatedCallCount);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
    }

    [Fact]
    public async Task BrowseAsyncHiddenGemsStaysOnCatalogEngineWhenFiltered()
    {
        var discoveryTracker = new DiscoveryServiceCallTracker();
        var movieTracker = new MovieDataProviderCallTracker();
        var service = CreateService(
            new DiscoverBrowseFakeCacheService(null),
            movieTracker,
            new TvShowDataProviderCallTracker(),
            discoveryTracker);

        var result = await service.BrowseAsync(
            CreateCriteria(SearchContentType.All, DiscoverBrowseMode.HiddenGems) with
            {
                MinRating = 8m
            },
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, discoveryTracker.HiddenGemsCallCount);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal("Hidden Gem", result.Items[0].Title);
    }

    [Fact]
    public async Task BrowseAsyncPopularKeepsProviderPopularityOrder()
    {
        var service = CreateService(
            new DiscoverBrowseFakeCacheService(null),
            new MovieDataProviderCallTracker(),
            new TvShowDataProviderCallTracker());

        var result = await service.BrowseAsync(
            CreateCriteria(SearchContentType.All, DiscoverBrowseMode.Popular, pageSize: 4),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            ["Discover Movie Beta", "Discover TV Beta", "Discover Movie Alpha", "Discover TV Alpha"],
            result.Items.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task BrowseAsyncPopular_ExcludesTalkGenreAndBackfillsEligibleTv()
    {
        var tvProvider = new TalkGenreSequencedTvProvider();
        var service = CreateService(
            new FakeMovieDataProvider(new MovieDataProviderCallTracker()),
            tvProvider);

        var rail = await service.BrowseAsync(
            DiscoverTitleRailCriteria.Create(DiscoverBrowseMode.Popular, 1, pageSize: 1) with
            {
                Type = SearchContentType.Tv
            },
            ContentLocaleResolver.EnglishUnitedStates);

        var seeAll = await service.BrowseAsync(
            CreateCriteria(SearchContentType.Tv, DiscoverBrowseMode.Popular, page: 1, pageSize: 1),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(seeAll.Items.Select(item => item.Title), rail.Items.Select(item => item.Title));
        Assert.Equal("Scripted Drama Page 2", rail.Items[0].Title);
        Assert.Equal(2, tvProvider.DiscoverCallCount);
        Assert.DoesNotContain(rail.Items, item => item.Title.Contains("Talk Headliner", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BrowseAsyncPopular_SeeAllPageTwoUsesSameTalkExclusionAsRail()
    {
        var tvProvider = new TalkGenreSequencedTvProvider();
        var service = CreateService(
            new FakeMovieDataProvider(new MovieDataProviderCallTracker()),
            tvProvider);

        var pageTwo = await service.BrowseAsync(
            CreateCriteria(SearchContentType.Tv, DiscoverBrowseMode.Popular, page: 2, pageSize: 1),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Scripted Drama Page 3", pageTwo.Items[0].Title);
        Assert.Equal(3, tvProvider.DiscoverCallCount);
        Assert.DoesNotContain(pageTwo.Items, item => item.Title.Contains("Talk Headliner", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BrowseAsyncTrendingWithFilters_DoesNotExcludeTalkGenre()
    {
        var tvProvider = new TalkGenreSequencedTvProvider();
        var service = CreateService(
            new FakeMovieDataProvider(new MovieDataProviderCallTracker()),
            tvProvider);

        var result = await service.BrowseAsync(
            CreateCriteria(SearchContentType.Tv, DiscoverBrowseMode.Trending, pageSize: 1) with
            {
                MinRating = 1m
            },
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Talk Headliner Page 1", result.Items[0].Title);
        Assert.Equal(1, tvProvider.DiscoverCallCount);
    }

    [Fact]
    public async Task BrowseAsyncTrendingUsesWeekListPagesInsteadOfDiscoverPopularity()
    {
        var weekList = new FakeTrendingWeekListService();
        var movieTracker = new MovieDataProviderCallTracker();
        var service = new DiscoverBrowseService(
            new DiscoveryServiceCallTracker(),
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(new TvShowDataProviderCallTracker()),
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            new FakeGenreReadRepository(),
            new FakeKeywordDiscoverReadRepository(),
            new DiscoverBrowseFakeCacheService(null),
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoverBrowseService>.Instance,
            new SearchItemCatalogMetadataEnricher(new FakeGenreReadRepository()),
            SearchTestDoubles.CreateDisplayTitleEnricher(),
            weekList,
            EmptyTrendingSnapshotService.Instance);

        var result = await service.BrowseAsync(
            CreateCriteria(SearchContentType.All, DiscoverBrowseMode.Trending, page: 2, pageSize: 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, weekList.CallCount);
        Assert.Equal(2, weekList.LastPage);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
        Assert.Contains(result.Items, item => item.Type == "movie");
        Assert.Contains(result.Items, item => item.Type == "tv");
        Assert.DoesNotContain(result.Items, item => item.Type == "person");
    }

    [Fact]
    public async Task BrowseAsyncNewReleasesWithFiltersUsesProviderPath()
    {
        var discoveryTracker = new DiscoveryServiceCallTracker();
        var movieTracker = new MovieDataProviderCallTracker();
        var service = CreateService(
            new DiscoverBrowseFakeCacheService(null),
            movieTracker,
            new TvShowDataProviderCallTracker(),
            discoveryTracker);

        var result = await service.BrowseAsync(
            new DiscoverBrowseCriteria(
                DiscoverBrowseMode.NewReleases,
                SearchContentType.Movie,
                [Guid.NewGuid()],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                [],
                null,
                1,
                20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(0, discoveryTracker.NewReleasesCallCount);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task BrowseAsyncWithUnresolvedKeywordFilterReturnsEmptyWithoutProviderCall()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var keywordRepository = new ConfigurableKeywordDiscoverReadRepository([]);
        var service = new DiscoverBrowseService(
            new DiscoveryServiceCallTracker(),
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(new TvShowDataProviderCallTracker()),
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            new FakeGenreReadRepository(),
            keywordRepository,
            cache,
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoverBrowseService>.Instance,
            new SearchItemCatalogMetadataEnricher(new FakeGenreReadRepository()),
            SearchTestDoubles.CreateDisplayTitleEnricher(),
            new FakeTrendingWeekListService(),
            EmptyTrendingSnapshotService.Instance);

        var keywordId = Guid.NewGuid();
        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.Trending,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [keywordId],
            [],
            null,
            1,
            20);

        var result = await service.BrowseAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal([keywordId], keywordRepository.LastResolvedKeywordIds);
    }

    [Fact]
    public async Task BrowseAsyncWithResolvedKeywordFilterCallsProvider()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var keywordRepository = new ConfigurableKeywordDiscoverReadRepository([1234]);
        var service = new DiscoverBrowseService(
            new DiscoveryServiceCallTracker(),
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(new TvShowDataProviderCallTracker()),
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            new FakeGenreReadRepository(),
            keywordRepository,
            cache,
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoverBrowseService>.Instance,
            new SearchItemCatalogMetadataEnricher(new FakeGenreReadRepository()),
            SearchTestDoubles.CreateDisplayTitleEnricher(),
            new FakeTrendingWeekListService(),
            EmptyTrendingSnapshotService.Instance);

        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.Trending,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [Guid.NewGuid()],
            [],
            null,
            1,
            20);

        var result = await service.BrowseAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEmpty(result.Items);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
    }

    [Fact]
    public async Task BrowseAsyncCachesSuccessfulResponses()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var service = CreateService(
            cache,
            movieTracker,
            new TvShowDataProviderCallTracker());
        var criteria = CreateCriteria(SearchContentType.Movie);

        await service.BrowseAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);
        await service.BrowseAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, cache.SetCount);
        Assert.Equal(3, cache.GetCount);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
    }

    [Fact]
    public async Task BrowseAsyncTurkishPopularUsesOneCanonicalDiscoverCallPerMediaType()
    {
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(
            new DiscoverBrowseFakeCacheService(null),
            movieTracker,
            tvTracker);

        await service.BrowseAsync(
            CreateCriteria(SearchContentType.All),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal(1, tvTracker.DiscoverTvShowsCallCount);
    }

    [Fact]
    public async Task BrowseAsyncSkipsGenreEnrichmentWhenDiscoveryAlreadyLocalizedTheRail()
    {
        var genres = new FakeGenreReadRepository();
        var service = new DiscoverBrowseService(
            new DiscoveryServiceCallTracker(),
            new FakeMovieDataProvider(new MovieDataProviderCallTracker()),
            new FakeTvShowDataProvider(new TvShowDataProviderCallTracker()),
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            genres,
            new FakeKeywordDiscoverReadRepository(),
            new DiscoverBrowseFakeCacheService(null),
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoverBrowseService>.Instance,
            new SearchItemCatalogMetadataEnricher(genres),
            SearchTestDoubles.CreateDisplayTitleEnricher(),
            new FakeTrendingWeekListService(),
            EmptyTrendingSnapshotService.Instance);

        await service.BrowseAsync(
            CreateCriteria(SearchContentType.Movie, DiscoverBrowseMode.NewReleases),
            ContentLocaleResolver.TurkishTurkey);
        await service.BrowseAsync(
            CreateCriteria(SearchContentType.Movie, DiscoverBrowseMode.TopRated),
            ContentLocaleResolver.TurkishTurkey);
        await service.BrowseAsync(
            CreateCriteria(SearchContentType.Movie, DiscoverBrowseMode.HiddenGems),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(0, genres.OrderedMovieGenreLookups);

        await service.BrowseAsync(
            CreateCriteria(SearchContentType.Movie, DiscoverBrowseMode.Popular),
            ContentLocaleResolver.TurkishTurkey);

        Assert.True(genres.OrderedMovieGenreLookups > 0);
    }

    [Fact]
    public async Task BrowseAsyncConcurrentMissesShareOneProviderLoad()
    {
        var coordinator = new DiscoveryCacheLoadCoordinator();
        var cache = new DiscoverBrowseFakeCacheService(null);
        var provider = new SlowMovieDiscoverProvider(TimeSpan.FromMilliseconds(250));
        var services = Enumerable.Range(0, 6)
            .Select(_ => new DiscoverBrowseService(
                new DiscoveryServiceCallTracker(),
                provider,
                new FakeTvShowDataProvider(new TvShowDataProviderCallTracker()),
                new SummaryMovieRepository(),
                new SummaryTvShowRepository(),
                new FakeGenreReadRepository(),
                new FakeKeywordDiscoverReadRepository(),
                cache,
                coordinator,
                NullLogger<DiscoverBrowseService>.Instance,
                new SearchItemCatalogMetadataEnricher(new FakeGenreReadRepository()),
                SearchTestDoubles.CreateDisplayTitleEnricher(),
                new FakeTrendingWeekListService(),
                EmptyTrendingSnapshotService.Instance))
            .ToArray();

        var criteria = CreateCriteria(SearchContentType.Movie);
        await Task.WhenAll(services.Select(service =>
            service.BrowseAsync(criteria, ContentLocaleResolver.TurkishTurkey)));

        Assert.Equal(1, provider.DiscoverMoviesCallCount);
    }

    private static DiscoverBrowseService CreateService(
        DiscoverBrowseFakeCacheService cache,
        MovieDataProviderCallTracker movieTracker,
        TvShowDataProviderCallTracker tvTracker,
        DiscoveryServiceCallTracker? discoveryTracker = null) =>
        CreateService(
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(tvTracker),
            cache,
            discoveryTracker);

    private static DiscoverBrowseService CreateService(
        IMovieDataProvider movieDataProvider,
        ITvShowDataProvider tvShowDataProvider,
        DiscoverBrowseFakeCacheService? cache = null,
        DiscoveryServiceCallTracker? discoveryTracker = null) =>
        new(
            discoveryTracker ?? new DiscoveryServiceCallTracker(),
            movieDataProvider,
            tvShowDataProvider,
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            new FakeGenreReadRepository(),
            new FakeKeywordDiscoverReadRepository(),
            cache ?? new DiscoverBrowseFakeCacheService(null),
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoverBrowseService>.Instance,
            new SearchItemCatalogMetadataEnricher(new FakeGenreReadRepository()),
            SearchTestDoubles.CreateDisplayTitleEnricher(),
            new FakeTrendingWeekListService(),
            EmptyTrendingSnapshotService.Instance);

    private static DiscoverBrowseCriteria CreateCriteria(
        SearchContentType type,
        DiscoverBrowseMode mode = DiscoverBrowseMode.Popular,
        int page = 1,
        int pageSize = 20,
        DiscoverBrowseSort? sort = null) =>
        new(
            mode,
            type,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            sort,
            page,
            pageSize);

    private static SearchItem CreateSearchItem(string type, Guid id, string title = "Title") =>
        new(
            id,
            type,
            title,
            null,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(2024, 1, 1),
            8.0m,
            100,
            2024);

    private sealed class DiscoverBrowseFakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();
        private readonly PaginatedResult<SearchItem>? _seededResult;

        public DiscoverBrowseFakeCacheService(PaginatedResult<SearchItem>? seededResult)
        {
            _seededResult = seededResult;
        }

        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            lock (_entries)
            {
                GetCount++;

                if (_seededResult is not null &&
                    typeof(T) == typeof(DiscoveryCacheEntry) &&
                    GetCount == 1)
                {
                    return Task.FromResult(new DiscoveryCacheEntry { Result = _seededResult } as T);
                }

                if (_entries.TryGetValue(key, out var value) && value is T typedValue)
                {
                    return Task.FromResult<T?>(typedValue);
                }

                return Task.FromResult<T?>(null);
            }
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            lock (_entries)
            {
                SetCount++;
                _entries[key] = value!;
                return Task.CompletedTask;
            }
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SlowMovieDiscoverProvider(TimeSpan delay) : IMovieDataProvider
    {
        private int _discoverMoviesCallCount;

        public int DiscoverMoviesCallCount => _discoverMoviesCallCount;

        public async Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _discoverMoviesCallCount);
            await Task.Delay(delay, cancellationToken);
            return new MovieProviderSearchResult(
                [
                    new MovieProviderSummary(
                        "movie-1",
                        1,
                        null,
                        null,
                        "Popular",
                        "Overview",
                        new DateOnly(2024, 1, 1),
                        "/poster.jpg",
                        8m,
                        20_000)
                ],
                criteria.Page,
                20,
                1,
                1);
        }

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SummaryMovieRepository : MovieApp.Application.Abstractions.Persistence.IMovieRepository
    {
        public int EnsureCount { get; private set; }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            EnsureCount++;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(
                        summary => summary.TmdbId!.Value,
                        summary => Guid.NewGuid()));
        }

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SummaryTvShowRepository : MovieApp.Application.Abstractions.Persistence.ITvShowRepository
    {
        public int EnsureCount { get; private set; }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            EnsureCount++;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(
                        summary => summary.TmdbId!.Value,
                        summary => Guid.NewGuid()));
        }

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class DiscoveryServiceCallTracker : IDiscoveryService
    {
        public int NewReleasesCallCount { get; private set; }

        public int CatalogListNewReleasesCallCount { get; private set; }

        public int TopRatedCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            NewReleasesCallCount++;
            return Task.FromResult(new PaginatedResult<SearchItem>(
                [CreateSearchItem("movie", Guid.NewGuid())],
                criteria.Page,
                criteria.PageSize,
                1,
                1));
        }

        public Task<PaginatedResult<SearchItem>> GetCatalogListNewReleasesAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            CatalogListNewReleasesCallCount++;
            return Task.FromResult(new PaginatedResult<SearchItem>(
                [CreateSearchItem("movie", Guid.NewGuid())],
                criteria.Page,
                criteria.PageSize,
                1,
                1));
        }

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            TopRatedCallCount++;
            return Task.FromResult(new PaginatedResult<SearchItem>(
                [CreateSearchItem("movie", Guid.NewGuid())],
                criteria.Page,
                criteria.PageSize,
                1,
                1));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int HiddenGemsCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetHiddenGemsAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            HiddenGemsCallCount++;
            return Task.FromResult(new PaginatedResult<SearchItem>(
                [CreateSearchItem("movie", Guid.NewGuid(), "Hidden Gem")],
                criteria.Page,
                criteria.PageSize,
                1,
                1));
        }
    }

    private sealed class EmptyTrendingSnapshotService : IHotThisWeekTrendingSnapshotService
    {
        public static EmptyTrendingSnapshotService Instance { get; } = new();

        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(null);

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTrendingWeekListService : ITrendingWeekListService
    {
        public int CallCount { get; private set; }

        public int LastPage { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPageAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPage = criteria.Page;
            return Task.FromResult(new PaginatedResult<SearchItem>(
                [
                    CreateSearchItem("movie", Guid.NewGuid(), "Week Movie"),
                    CreateSearchItem("tv", Guid.NewGuid(), "Week Show")
                ],
                criteria.Page,
                criteria.PageSize,
                40,
                2));
        }
    }

    private sealed class FakeGenreReadRepository : MovieApp.Application.Abstractions.Persistence.IGenreReadRepository
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

        public int OrderedMovieGenreLookups { get; private set; }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
            IReadOnlyList<Guid> movieIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default)
        {
            OrderedMovieGenreLookups++;
            return GenreReadRepositoryTestDefaults.EmptyMovieGenresAsync(movieIds, maxGenresPerItem, cancellationToken);
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            GenreReadRepositoryTestDefaults.EmptyTvGenresAsync(tvShowIds, maxGenresPerItem, cancellationToken);
    }

    private sealed class FakeKeywordDiscoverReadRepository() : ConfigurableKeywordDiscoverReadRepository([]);

    private class ConfigurableKeywordDiscoverReadRepository(IReadOnlyList<int> resolvedTmdbIds)
        : MovieApp.Application.Abstractions.Persistence.IKeywordDiscoverReadRepository
    {
        public IReadOnlyList<Guid> LastResolvedKeywordIds { get; private set; } = [];

        public Task<MovieApp.Application.Models.Movies.PaginatedResult<MovieApp.Application.Models.Discovery.KeywordDiscoverItem>> SearchAsync(
            string query,
            string contentLocale,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieApp.Application.Models.Movies.PaginatedResult<MovieApp.Application.Models.Discovery.KeywordDiscoverItem>(
                [], page, pageSize, 0, 0));

        public Task<IReadOnlyList<int>> ResolveTmdbKeywordIdsAsync(
            IReadOnlyList<Guid> keywordIds,
            CancellationToken cancellationToken = default)
        {
            LastResolvedKeywordIds = keywordIds;
            return Task.FromResult(resolvedTmdbIds);
        }
    }

    private sealed class CappedDiscoverMovieProvider(int itemsPerPage, int totalPages) : IMovieDataProvider
    {
        public int DiscoverCallCount { get; private set; }

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            DiscoverCallCount++;
            return Task.FromResult(new MovieProviderSearchResult(
                CreatePage(criteria.Page, idOffset: 100_000),
                criteria.Page,
                itemsPerPage,
                totalPages * itemsPerPage,
                totalPages));
        }

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private List<MovieProviderSummary> CreatePage(int page, int idOffset)
        {
            if (page > totalPages)
            {
                return [];
            }

            var start = idOffset + ((page - 1) * itemsPerPage);
            return Enumerable.Range(start, itemsPerPage)
                .Select(tmdbId => new MovieProviderSummary(
                    $"movie-{tmdbId}",
                    tmdbId,
                    null,
                    null,
                    $"Movie {tmdbId}",
                    "Overview",
                    new DateOnly(2024, 1, 1),
                    "/poster.jpg",
                    8.0m,
                    tmdbId))
                .ToList();
        }
    }

    private sealed class TalkGenreSequencedTvProvider : ITvShowDataProvider
    {
        public int DiscoverCallCount { get; private set; }

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            DiscoverCallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            var page = criteria.Page;
            TvShowProviderSummary summary = page switch
            {
                1 => CreateSummary(
                    900_001,
                    "Talk Headliner Page 1",
                    popularity: 500m,
                    genreIds: [OnTvThisWeekContentSelector.TalkTmdbGenreId]),
                2 => CreateSummary(
                    900_002,
                    "Scripted Drama Page 2",
                    popularity: 100m,
                    genreIds: [18]),
                _ => CreateSummary(
                    900_000 + page,
                    $"Scripted Drama Page {page}",
                    popularity: 90m - page,
                    genreIds: [18])
            };

            return Task.FromResult(new TvShowProviderSearchResult(
                [summary],
                page,
                1,
                10,
                10));
        }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static TvShowProviderSummary CreateSummary(
            int tmdbId,
            string title,
            decimal popularity,
            IReadOnlyList<int> genreIds) =>
            new(
                $"tv-{tmdbId}",
                tmdbId,
                null,
                null,
                title,
                title,
                "Overview",
                new DateOnly(2024, 1, 1),
                "/poster.jpg",
                null,
                "en",
                8m,
                200,
                popularity,
                null,
                genreIds);
    }

    private sealed class CappedDiscoverTvProvider(int itemsPerPage, int totalPages) : ITvShowDataProvider
    {
        public int DiscoverCallCount { get; private set; }

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            DiscoverCallCount++;
            return Task.FromResult(new TvShowProviderSearchResult(
                CreatePage(criteria.Page, idOffset: 200_000),
                criteria.Page,
                itemsPerPage,
                totalPages * itemsPerPage,
                totalPages));
        }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private List<TvShowProviderSummary> CreatePage(int page, int idOffset)
        {
            if (page > totalPages)
            {
                return [];
            }

            var start = idOffset + ((page - 1) * itemsPerPage);
            return Enumerable.Range(start, itemsPerPage)
                .Select(tmdbId => new TvShowProviderSummary(
                    $"tv-{tmdbId}",
                    tmdbId,
                    null,
                    null,
                    $"Show {tmdbId}",
                    $"Show {tmdbId}",
                    "Overview",
                    new DateOnly(2024, 1, 1),
                    "/poster.jpg",
                    null,
                    "en",
                    8.0m,
                    tmdbId))
                .ToList();
        }
    }
}
