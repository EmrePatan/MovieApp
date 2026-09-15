using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class DiscoveryWatchProvidersServiceTests
{
    [Fact]
    public async Task GetWatchProvidersAsyncReturnsCachedProvidersWithoutCallingCatalog()
    {
        var catalog = new RecordingDiscoveryWatchProviderCatalog();
        var cache = new DiscoveryWatchProvidersFakeCache();
        var service = new DiscoveryWatchProvidersService(catalog, cache);
        var providers = new List<DiscoveryWatchProviderItem>
        {
            new(8, "Netflix", "/logo.png", 1),
        };

        await cache.SetAsync(
            DiscoveryWatchProvidersCacheKeys.Create(SearchContentType.Movie, "TR"),
            new DiscoveryWatchProvidersCacheEntry { Providers = providers },
            TimeSpan.FromHours(1));

        var result = await service.GetWatchProvidersAsync(SearchContentType.Movie, "TR");

        Assert.Single(result);
        Assert.Equal(0, catalog.CallCount);
    }

    [Fact]
    public async Task GetWatchProvidersAsyncCachesCatalogResults()
    {
        var catalog = new RecordingDiscoveryWatchProviderCatalog();
        var cache = new DiscoveryWatchProvidersFakeCache();
        var service = new DiscoveryWatchProvidersService(catalog, cache);

        var result = await service.GetWatchProvidersAsync(SearchContentType.Tv, "tr");

        Assert.Equal(2, result.Count);
        Assert.Equal(1, catalog.CallCount);
        Assert.Equal(SearchContentType.Tv, catalog.LastMediaType);
        Assert.Equal("TR", catalog.LastRegion);
    }

    [Fact]
    public async Task GetWatchProvidersAsyncThrowsWhenCatalogFails()
    {
        var catalog = new RecordingDiscoveryWatchProviderCatalog { ShouldThrow = true };
        var service = new DiscoveryWatchProvidersService(catalog, new DiscoveryWatchProvidersFakeCache());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(
            () => service.GetWatchProvidersAsync(SearchContentType.Movie, "TR"));
    }

    private sealed class RecordingDiscoveryWatchProviderCatalog : IDiscoveryWatchProviderCatalog
    {
        public int CallCount { get; private set; }

        public SearchContentType LastMediaType { get; private set; }

        public string LastRegion { get; private set; } = string.Empty;

        public bool ShouldThrow { get; init; }

        public Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
            SearchContentType mediaType,
            string watchRegion,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastMediaType = mediaType;
            LastRegion = watchRegion;

            if (ShouldThrow)
            {
                throw new InvalidOperationException("provider failed");
            }

            return Task.FromResult<IReadOnlyList<DiscoveryWatchProviderItem>>(
            [
                new DiscoveryWatchProviderItem(8, "Netflix", "/logo.png", 1),
                new DiscoveryWatchProviderItem(337, "Disney Plus", "/logo.png", 2),
            ]);
        }
    }

    private sealed class DiscoveryWatchProvidersFakeCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (_entries.TryGetValue(key, out var value) && value is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
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
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
