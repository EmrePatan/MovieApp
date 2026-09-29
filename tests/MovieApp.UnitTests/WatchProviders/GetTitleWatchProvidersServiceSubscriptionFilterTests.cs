using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.WatchProviders;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.WatchProviders;

public sealed class GetTitleWatchProvidersServiceSubscriptionFilterTests
{
    private static readonly Guid CatalogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    [Fact]
    public async Task GetMovieWatchProvidersAsync_applies_subscription_filter()
    {
        var service = CreateMovieService(
            new WatchProvidersResult(
                "TR",
                [
                    Provider(1, [WatchProviderAvailabilityType.Flatrate]),
                    Provider(2, [WatchProviderAvailabilityType.Rent]),
                ],
                null));

        var result = await service.GetWatchProvidersAsync(CatalogId, "TR");

        Assert.Single(result.Providers);
        Assert.Equal(1, result.Providers[0].ProviderId);
        Assert.Equal([WatchProviderAvailabilityType.Flatrate], result.Providers[0].AvailabilityTypes);
    }

    [Fact]
    public async Task GetTvShowWatchProvidersAsync_applies_subscription_filter()
    {
        var service = CreateTvService(
            new WatchProvidersResult(
                "TR",
                [
                    Provider(3, [WatchProviderAvailabilityType.Flatrate, WatchProviderAvailabilityType.Buy]),
                    Provider(4, [WatchProviderAvailabilityType.Ads]),
                ],
                null));

        var result = await service.GetWatchProvidersAsync(CatalogId, "TR");

        Assert.Single(result.Providers);
        Assert.Equal(3, result.Providers[0].ProviderId);
        Assert.Equal([WatchProviderAvailabilityType.Flatrate], result.Providers[0].AvailabilityTypes);
    }

    [Fact]
    public async Task GetMovieWatchProvidersAsync_filters_cached_unfiltered_results_on_read()
    {
        var cache = new InMemoryWatchProvidersCache();
        await cache.SetAsync(
            MovieWatchProvidersCacheKeys.Create(CatalogId, "TR"),
            new WatchProvidersCacheEntry
            {
                Result = new WatchProvidersResult(
                    "TR",
                    [Provider(9, [WatchProviderAvailabilityType.Flatrate, WatchProviderAvailabilityType.Rent])],
                    null),
            },
            TimeSpan.FromHours(6));

        var service = new GetMovieWatchProvidersService(
            new FakeMovieRepository(CatalogId, 100),
            new ThrowingWatchProviderService(),
            cache);

        var result = await service.GetWatchProvidersAsync(CatalogId, "TR");

        Assert.Single(result.Providers);
        Assert.Equal([WatchProviderAvailabilityType.Flatrate], result.Providers[0].AvailabilityTypes);
    }

    private static GetMovieWatchProvidersService CreateMovieService(WatchProvidersResult providerResult) =>
        new(
            new FakeMovieRepository(CatalogId, 100),
            new StubWatchProviderService(providerResult),
            new InMemoryWatchProvidersCache());

    private static GetTvShowWatchProvidersService CreateTvService(WatchProvidersResult providerResult) =>
        new(
            new FakeTvShowRepository(CatalogId, 200),
            new StubWatchProviderService(providerResult),
            new InMemoryWatchProvidersCache());

    private static WatchProviderResult Provider(
        int providerId,
        IReadOnlyList<WatchProviderAvailabilityType> availabilityTypes) =>
        new(providerId, $"Provider {providerId}", null, providerId, availabilityTypes, null);

    private sealed class FakeMovieRepository(Guid id, int tmdbId) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(
                movieId == id
                    ? new Movie { Id = id, TmdbId = tmdbId, Title = "Test" }
                    : null);

        public Task<Movie?> GetByTmdbIdAsync(int lookupTmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(lookupTmdbId == tmdbId ? new Movie { Id = id, TmdbId = tmdbId, Title = "Test" } : null);

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository(Guid id, int tmdbId) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(
                tvShowId == id
                    ? new TvShow { Id = id, TmdbId = tmdbId, Title = "Test" }
                    : null);

        public Task<TvShow?> GetByTmdbIdAsync(int lookupTmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(lookupTmdbId == tmdbId ? new TvShow { Id = id, TmdbId = tmdbId, Title = "Test" } : null);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubWatchProviderService(WatchProvidersResult result) : IWatchProviderService
    {
        public Task<WatchProvidersResult> GetMovieWatchProvidersAsync(
            int tmdbId,
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);

        public Task<WatchProvidersResult> GetTvShowWatchProvidersAsync(
            int tmdbId,
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingWatchProviderService : IWatchProviderService
    {
        public Task<WatchProvidersResult> GetMovieWatchProvidersAsync(
            int tmdbId,
            string region,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Cache should have been used.");

        public Task<WatchProvidersResult> GetTvShowWatchProvidersAsync(
            int tmdbId,
            string region,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Cache should have been used.");
    }

    private sealed class InMemoryWatchProvidersCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
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
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
