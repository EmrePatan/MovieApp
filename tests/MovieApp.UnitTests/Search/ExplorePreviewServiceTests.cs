using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class ExplorePreviewServiceTests
{
    [Fact]
    public async Task GetPreviewAsyncReturnsTrendingTopRatedAndNewReleases()
    {
        var discovery = new FakeDiscoveryService();
        var service = new ExplorePreviewService(discovery, new PassthroughCacheService());

        var result = await service.GetPreviewAsync(new ExplorePreviewCriteria(10));

        Assert.NotEmpty(result.Trending.Items);
        Assert.NotEmpty(result.TopRated.Items);
        Assert.NotEmpty(result.NewReleases.Items);
        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
        Assert.Equal(0, discovery.PopularCallCount);
        Assert.Equal(0, discovery.GenreCallCount);
    }

    [Fact]
    public async Task GetPreviewAsyncUsesCacheOnSecondRequest()
    {
        var discovery = new FakeDiscoveryService();
        var cache = new InMemoryCacheService();
        var service = new ExplorePreviewService(discovery, cache);

        await service.GetPreviewAsync(new ExplorePreviewCriteria(10));
        await service.GetPreviewAsync(new ExplorePreviewCriteria(10));

        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
    }

    [Fact]
    public async Task GetPreviewAsyncRejectsInvalidSectionSize()
    {
        var service = new ExplorePreviewService(
            new FakeDiscoveryService(),
            new PassthroughCacheService());

        await Assert.ThrowsAsync<MovieApp.Application.Exceptions.ValidationException>(() =>
            service.GetPreviewAsync(new ExplorePreviewCriteria(25)));
    }

    private sealed class FakeDiscoveryService : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public int TopRatedCallCount { get; private set; }

        public int NewReleasesCallCount { get; private set; }

        public int PopularCallCount { get; private set; }

        public int GenreCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            PopularCallCount++;
            return Task.FromResult(CreatePage("popular"));
        }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            return Task.FromResult(CreatePage("trending"));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            NewReleasesCallCount++;
            return Task.FromResult(CreatePage("new-releases"));
        }

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            TopRatedCallCount++;
            return Task.FromResult(CreatePage("top-rated"));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            GenreCallCount++;
            return Task.FromResult(CreatePage(genreName));
        }

        private static PaginatedResult<SearchItem> CreatePage(string label) =>
            new([CreateItem(label)], 1, 10, 1, 1);

        private static SearchItem CreateItem(string label) =>
            new(
                Guid.NewGuid(),
                "movie",
                label,
                null,
                null,
                null,
                null,
                new DateOnly(2020, 1, 1),
                8m,
                100,
                2020);
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

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (_entries.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
