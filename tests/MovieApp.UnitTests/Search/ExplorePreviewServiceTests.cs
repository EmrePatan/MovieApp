using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class ExplorePreviewServiceTests
{
    [Fact]
    public async Task GetPreviewAsyncReturnsTrendingTopRatedAndNewReleases()
    {
        var discovery = new FakeDiscoveryService();
        var service = CreateService(discovery, new PassthroughCacheService());

        var result = await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);

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
        var service = CreateService(discovery, cache);

        await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);
        await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
    }

    [Fact]
    public async Task GetPreviewAsyncRejectsInvalidSectionSize()
    {
        var service = CreateService(
            new FakeDiscoveryService(),
            new PassthroughCacheService());

        await Assert.ThrowsAsync<MovieApp.Application.Exceptions.ValidationException>(() =>
            service.GetPreviewAsync(new ExplorePreviewCriteria(25), ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task GetPreviewAsyncLoadsDiscoverySectionsInIndependentScopes()
    {
        var service = CreateServiceWithScopedDiscovery<ConcurrentDiscoveryService>(
            new PassthroughCacheService());

        var result = await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEmpty(result.Trending.Items);
        Assert.NotEmpty(result.TopRated.Items);
        Assert.NotEmpty(result.NewReleases.Items);
    }

    private static ExplorePreviewService CreateService(
        IDiscoveryService discovery,
        ICacheService cache)
    {
        var services = new ServiceCollection();
        services.AddScoped<IDiscoveryService>(_ => discovery);
        var provider = services.BuildServiceProvider();

        return new ExplorePreviewService(provider.GetRequiredService<IServiceScopeFactory>(), cache);
    }

    private static ExplorePreviewService CreateServiceWithScopedDiscovery<TDiscovery>(
        ICacheService cache)
        where TDiscovery : class, IDiscoveryService
    {
        var services = new ServiceCollection();
        services.AddScoped<IDiscoveryService, TDiscovery>();
        var provider = services.BuildServiceProvider();

        return new ExplorePreviewService(provider.GetRequiredService<IServiceScopeFactory>(), cache);
    }

    private sealed class FakeDiscoveryService : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public int TopRatedCallCount { get; private set; }

        public int NewReleasesCallCount { get; private set; }

        public int PopularCallCount { get; private set; }

        public int GenreCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            PopularCallCount++;
            return Task.FromResult(CreatePage("popular"));
        }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            return Task.FromResult(CreatePage("trending"));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            NewReleasesCallCount++;
            return Task.FromResult(CreatePage("new-releases"));
        }

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TopRatedCallCount++;
            return Task.FromResult(CreatePage("top-rated"));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
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

    private sealed class ConcurrentDiscoveryService : IDiscoveryService
    {
        private int _activeOperations;

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            SimulateDbOperationAsync("popular", cancellationToken);

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            SimulateDbOperationAsync("trending", cancellationToken);

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            SimulateDbOperationAsync("new-releases", cancellationToken);

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            SimulateDbOperationAsync("top-rated", cancellationToken);

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            SimulateDbOperationAsync(genreName, cancellationToken);

        private async Task<PaginatedResult<SearchItem>> SimulateDbOperationAsync(
            string label,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _activeOperations) > 1)
            {
                Interlocked.Decrement(ref _activeOperations);
                throw new InvalidOperationException(
                    "An attempt was made to use the context instance while it is being configured. " +
                    "This can happen if a second operation is started on this context instance before " +
                    "a previous operation completed.");
            }

            try
            {
                await Task.Delay(25, cancellationToken);
                return new PaginatedResult<SearchItem>(
                    [CreateItem(label)],
                    1,
                    10,
                    1,
                    1);
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        }

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
