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
    public async Task GetPreviewAsyncReturnsDiscoverRailsWithoutGeneralTrending()
    {
        var discovery = new FakeDiscoveryService();
        var browse = new FakeDiscoverBrowseService();
        var service = CreateService(discovery, browse, new PassthroughCacheService());

        var result = await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(DiscoverRailCatalog.Order, result.RailOrder);
        Assert.NotEmpty(result.HiddenGems.Items);
        Assert.NotEmpty(result.Popular.Items);
        Assert.NotEmpty(result.NewReleases.Items);
        Assert.NotEmpty(result.TopRated.Items);
        Assert.Equal(1, browse.HiddenGemsCallCount);
        Assert.Equal(1, browse.PopularCallCount);
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Equal(0, discovery.PopularCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
        Assert.Equal(0, discovery.GenreCallCount);
    }

    [Fact]
    public async Task GetPreviewAsyncUsesCacheOnSecondRequest()
    {
        var discovery = new FakeDiscoveryService();
        var browse = new FakeDiscoverBrowseService();
        var cache = new InMemoryCacheService();
        var service = CreateService(discovery, browse, cache);

        await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);
        await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, browse.HiddenGemsCallCount);
        Assert.Equal(1, browse.PopularCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
        Assert.Equal(0, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task GetPreviewAsyncRejectsInvalidSectionSize()
    {
        var service = CreateService(
            new FakeDiscoveryService(),
            new FakeDiscoverBrowseService(),
            new PassthroughCacheService());

        await Assert.ThrowsAsync<MovieApp.Application.Exceptions.ValidationException>(() =>
            service.GetPreviewAsync(new ExplorePreviewCriteria(25), ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task GetPreviewAsyncUsesSameBrowseCriteriaAsSeeAllDefaults()
    {
        var browse = new RecordingDiscoverBrowseService();
        var service = CreateService(new FakeDiscoveryService(), browse, new PassthroughCacheService());

        await service.GetPreviewAsync(new ExplorePreviewCriteria(12), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            DiscoverTitleRailCriteria.Create(DiscoverBrowseMode.HiddenGems, 1, 12),
            browse.LastHiddenGemsCriteria);
        Assert.Equal(
            DiscoverTitleRailCriteria.Create(DiscoverBrowseMode.Popular, 1, 12),
            browse.LastPopularCriteria);
    }

    [Fact]
    public async Task GetPreviewAsyncLoadsDiscoverySectionsInIndependentScopes()
    {
        var service = CreateServiceWithScopedDiscovery<ConcurrentDiscoveryService>(
            new PassthroughCacheService());

        var result = await service.GetPreviewAsync(new ExplorePreviewCriteria(10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(DiscoverRailCatalog.Order, result.RailOrder);
        Assert.NotEmpty(result.HiddenGems.Items);
        Assert.NotEmpty(result.Popular.Items);
        Assert.NotEmpty(result.TopRated.Items);
        Assert.NotEmpty(result.NewReleases.Items);
    }

    private static ExplorePreviewService CreateService(
        IDiscoveryService discovery,
        IDiscoverBrowseService browse,
        ICacheService cache)
    {
        var services = new ServiceCollection();
        services.AddScoped<IDiscoveryService>(_ => discovery);
        services.AddScoped<IDiscoverBrowseService>(_ => browse);
        var provider = services.BuildServiceProvider();

        return new ExplorePreviewService(provider.GetRequiredService<IServiceScopeFactory>(), cache);
    }

    private static ExplorePreviewService CreateServiceWithScopedDiscovery<TDiscovery>(
        ICacheService cache)
        where TDiscovery : class, IDiscoveryService
    {
        var services = new ServiceCollection();
        services.AddScoped<IDiscoveryService, TDiscovery>();
        services.AddScoped<IDiscoverBrowseService, FakeDiscoverBrowseService>();
        var provider = services.BuildServiceProvider();

        return new ExplorePreviewService(provider.GetRequiredService<IServiceScopeFactory>(), cache);
    }

    private class RecordingDiscoverBrowseService : IDiscoverBrowseService
    {
        public int HiddenGemsCallCount { get; private set; }

        public int PopularCallCount { get; private set; }

        public DiscoverBrowseCriteria? LastHiddenGemsCriteria { get; private set; }

        public DiscoverBrowseCriteria? LastPopularCriteria { get; private set; }

        public Task<PaginatedResult<SearchItem>> BrowseAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            if (criteria.Mode == DiscoverBrowseMode.HiddenGems)
            {
                HiddenGemsCallCount++;
                LastHiddenGemsCriteria = criteria;
            }
            else if (criteria.Mode == DiscoverBrowseMode.Popular)
            {
                PopularCallCount++;
                LastPopularCriteria = criteria;
            }

            return Task.FromResult(new PaginatedResult<SearchItem>(
                [CreateBrowseItem(criteria.Mode.ToString())],
                1,
                10,
                1,
                1));
        }

        private static SearchItem CreateBrowseItem(string label) =>
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

    private sealed class FakeDiscoverBrowseService : RecordingDiscoverBrowseService;

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
