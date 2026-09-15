using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.People;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Images;

public sealed class GetPersonImagesServiceTests
{
    [Fact]
    public async Task GetImagesAsyncReturnsProfilesForKnownPerson()
    {
        var service = new GetPersonImagesService(new FakeImageProvider(), new InMemoryCacheService());

        var result = await service.GetImagesAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.Equal(2, result.Profiles.Count);
        Assert.Equal("/fake/cooper-profile-1.jpg", result.Profiles[0].FilePath);
        Assert.Empty(result.Backdrops);
    }

    [Fact]
    public async Task GetImagesAsyncReturnsEmptyForUnknownPerson()
    {
        var service = new GetPersonImagesService(new FakeImageProvider(), new InMemoryCacheService());

        var result = await service.GetImagesAsync(999999);

        Assert.Empty(result.Profiles);
    }

    [Fact]
    public async Task GetImagesAsyncThrowsValidationForInvalidId()
    {
        var service = new GetPersonImagesService(new FakeImageProvider(), new InMemoryCacheService());

        await Assert.ThrowsAsync<ValidationException>(() => service.GetImagesAsync(0));
    }

    [Fact]
    public async Task GetImagesAsyncUsesCacheAndSkipsProvider()
    {
        var provider = new CountingImageProvider();
        var cache = new InMemoryCacheService();
        var service = new GetPersonImagesService(provider, cache);

        await service.GetImagesAsync(FakePersonDataProvider.McConaugheyTmdbId);
        await service.GetImagesAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.Equal(1, provider.PersonCallCount);
    }

    [Fact]
    public async Task GetImagesAsyncReturnsEmptyOnProviderFailureWithoutCaching()
    {
        var provider = new ThrowingImageProvider();
        var cache = new InMemoryCacheService();
        var service = new GetPersonImagesService(provider, cache);

        var result = await service.GetImagesAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.Empty(result.Profiles);
        Assert.Null(await cache.GetAsync<ImagesCacheEntry>(
            PersonImagesCacheKeys.Create(FakePersonDataProvider.McConaugheyTmdbId)));
    }

    private sealed class CountingImageProvider : IImageProvider
    {
        public int PersonCallCount { get; private set; }

        public Task<ProviderImagesResult?> GetMovieImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);

        public Task<ProviderImagesResult?> GetTvShowImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);

        public Task<ProviderImagesResult?> GetPersonImagesAsync(int tmdbPersonId, CancellationToken cancellationToken = default)
        {
            PersonCallCount += 1;
            return Task.FromResult<ProviderImagesResult?>(FakeImageProvider.McConaugheyImages);
        }
    }

    private sealed class ThrowingImageProvider : IImageProvider
    {
        public Task<ProviderImagesResult?> GetMovieImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);

        public Task<ProviderImagesResult?> GetTvShowImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);

        public Task<ProviderImagesResult?> GetPersonImagesAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            throw new TmdbApiException(System.Net.HttpStatusCode.ServiceUnavailable, "TMDB unavailable.");
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (_entries.TryGetValue(key, out var value))
            {
                return Task.FromResult((T?)value);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
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
