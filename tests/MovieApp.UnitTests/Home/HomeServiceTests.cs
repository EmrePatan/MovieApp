using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
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
            [
                HomeSectionType.Trending,
                HomeSectionType.Popular,
                HomeSectionType.NewReleases,
                HomeSectionType.TopRated,
                HomeSectionType.Genre
            ],
            result.Sections.Select(section => section.Type).ToList());
        Assert.True(cache.WasWritten);
    }

    [Fact]
    public async Task GetHomeAsyncBuildsPersonalizedSectionsInOrder()
    {
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

        var result = await service.GetHomeAsync(new HomeCriteria(SearchContentType.All, 5));

        Assert.True(result.IsPersonalized);
        Assert.Equal(
            [
                HomeSectionType.ContinueWatching,
                HomeSectionType.RecommendedForYou,
                HomeSectionType.BecauseYouWatched,
                HomeSectionType.BasedOnFavorites,
                HomeSectionType.Trending,
                HomeSectionType.Popular,
                HomeSectionType.NewReleases,
                HomeSectionType.TopRated,
                HomeSectionType.Genre
            ],
            result.Sections.Select(section => section.Type).ToList());
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
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.BasedOnFavorites);
        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.ContinueWatching);
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

        Assert.DoesNotContain(result.Sections, section => section.Type == HomeSectionType.ContinueWatching);
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

    private static HomeService CreateService(
        FakeCacheService? cache = null,
        IRecommendationService? recommendationService = null,
        IDiscoveryService? discoveryService = null,
        IWatchHistoryService? watchHistoryService = null,
        HomeOptions? options = null)
    {
        return new HomeService(
            new FakeCurrentUser(UserId),
            CreateScopeFactory(recommendationService, discoveryService, watchHistoryService),
            cache ?? new FakeCacheService(),
            Options.Create(options ?? new HomeOptions
            {
                DefaultSectionSize = 10,
                MaximumSectionSize = 20,
                GenreSections = ["Science Fiction"]
            }));
    }

    private static IServiceScopeFactory CreateScopeFactory(
        IRecommendationService? recommendationService = null,
        IDiscoveryService? discoveryService = null,
        IWatchHistoryService? watchHistoryService = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICurrentUser>(_ => new FakeCurrentUser(UserId));
        services.AddScoped<IRecommendationService>(_ =>
            recommendationService ?? new FakeRecommendationService([]));
        services.AddScoped<IDiscoveryService>(_ =>
            discoveryService ?? new FakeDiscoveryService());
        services.AddScoped<IWatchHistoryService>(_ =>
            watchHistoryService ?? new FakeWatchHistoryService([]));

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
            CancellationToken cancellationToken = default) =>
            Task.FromCanceled<IReadOnlyList<RecommendationSection>>(cancellationToken);
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
}
