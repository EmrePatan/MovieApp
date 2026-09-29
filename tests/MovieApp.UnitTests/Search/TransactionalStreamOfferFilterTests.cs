using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.WatchProviders;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class TransactionalStreamOfferFilterTests
{
    [Fact]
    public async Task SelectMatchingTmdbIdsAsyncDropsStoreStreamTitlesThatAreNotFlatrateOnThatStore()
    {
        var watchProviders = new StubWatchProviderService();
        watchProviders.MovieOffers[101] = Offers(
            Provider(337, [WatchProviderAvailabilityType.Flatrate]),
            Provider(2, [WatchProviderAvailabilityType.Buy, WatchProviderAvailabilityType.Rent]));
        watchProviders.MovieOffers[202] = Offers(
            Provider(2, [WatchProviderAvailabilityType.Flatrate]));
        var filter = CreateFilter(watchProviders);

        var matching = await filter.SelectMatchingTmdbIdsAsync(
            SearchContentType.Movie,
            "TR",
            [2],
            [WatchMonetizationType.Stream],
            [101, 202]);

        Assert.Equal([202], matching.OrderBy(id => id).ToArray());
        Assert.Equal(2, watchProviders.MovieCallCount);
    }

    [Fact]
    public async Task SelectMatchingTmdbIdsAsyncKeepsSubscriptionFlatrateWhenStoreIsAlsoSelected()
    {
        var watchProviders = new StubWatchProviderService();
        watchProviders.MovieOffers[303] = Offers(
            Provider(8, [WatchProviderAvailabilityType.Flatrate]),
            Provider(2, [WatchProviderAvailabilityType.Rent]));
        watchProviders.MovieOffers[404] = Offers(
            Provider(337, [WatchProviderAvailabilityType.Flatrate]),
            Provider(10, [WatchProviderAvailabilityType.Buy]));
        var filter = CreateFilter(watchProviders);

        var matching = await filter.SelectMatchingTmdbIdsAsync(
            SearchContentType.Movie,
            "us",
            [8, 10],
            [WatchMonetizationType.Stream],
            [303, 404]);

        Assert.Equal([303], matching.ToArray());
    }

    [Fact]
    public async Task SelectMatchingTmdbIdsAsyncSkipsOfferLookupWhenRequestIsNotStoreStream()
    {
        var watchProviders = new StubWatchProviderService();
        var filter = CreateFilter(watchProviders);

        var subscription = await filter.SelectMatchingTmdbIdsAsync(
            SearchContentType.Movie,
            "TR",
            [8],
            [WatchMonetizationType.Stream],
            [1, 2]);
        var storeRent = await filter.SelectMatchingTmdbIdsAsync(
            SearchContentType.Tv,
            "TR",
            [2, 3],
            [WatchMonetizationType.Rent],
            [7]);

        Assert.Equal([1, 2], subscription.OrderBy(id => id).ToArray());
        Assert.Equal([7], storeRent.ToArray());
        Assert.Equal(0, watchProviders.MovieCallCount);
        Assert.Equal(0, watchProviders.TvCallCount);
    }

    private static TransactionalStreamOfferFilter CreateFilter(StubWatchProviderService watchProviders) =>
        new(
            watchProviders,
            new MemoryCacheService(),
            Options.Create(new AdvancedDiscoverOptions()));

    private static WatchProvidersResult Offers(params WatchProviderResult[] providers) =>
        new("TR", providers, null);

    private static WatchProviderResult Provider(
        int providerId,
        IReadOnlyList<WatchProviderAvailabilityType> availabilityTypes) =>
        new(providerId, $"Provider {providerId}", null, providerId, availabilityTypes, null);

    private sealed class StubWatchProviderService : IWatchProviderService
    {
        public Dictionary<int, WatchProvidersResult> MovieOffers { get; } = [];

        public Dictionary<int, WatchProvidersResult> TvOffers { get; } = [];

        public int MovieCallCount { get; private set; }

        public int TvCallCount { get; private set; }

        public Task<WatchProvidersResult> GetMovieWatchProvidersAsync(
            int tmdbId,
            string region,
            CancellationToken cancellationToken = default)
        {
            MovieCallCount++;
            return Task.FromResult(MovieOffers.TryGetValue(tmdbId, out var offers)
                ? offers
                : new WatchProvidersResult(region, [], null));
        }

        public Task<WatchProvidersResult> GetTvShowWatchProvidersAsync(
            int tmdbId,
            string region,
            CancellationToken cancellationToken = default)
        {
            TvCallCount++;
            return Task.FromResult(TvOffers.TryGetValue(tmdbId, out var offers)
                ? offers
                : new WatchProvidersResult(region, [], null));
        }
    }

    private sealed class MemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

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

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
