using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Images;

public sealed class GetTvShowImagesServiceTests
{
    [Fact]
    public async Task GetImagesAsyncReturnsOrderedGalleryForTvShow()
    {
        var tvShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repository = new FakeTvShowRepository
        {
            TvShow = new TvShow
            {
                Id = tvShowId,
                TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
                Title = "Breaking Bad",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var service = new GetTvShowImagesService(repository, new FakeImageProvider(), new InMemoryCacheService());

        var result = await service.GetImagesAsync(tvShowId, "en");

        Assert.Single(result.Backdrops);
        Assert.Single(result.Posters);
        Assert.Equal("/fake/breaking-bad-poster.jpg", result.Posters[0].FilePath);
    }

    [Fact]
    public async Task GetImagesAsyncReturnsEmptyWhenNoTmdbId()
    {
        var tvShowId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var repository = new FakeTvShowRepository
        {
            TvShow = new TvShow
            {
                Id = tvShowId,
                TmdbId = null,
                Title = "No Provider",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var service = new GetTvShowImagesService(repository, new FakeImageProvider(), new InMemoryCacheService());

        var result = await service.GetImagesAsync(tvShowId, "en");

        Assert.Empty(result.Backdrops);
        Assert.Empty(result.Posters);
    }

    [Fact]
    public async Task GetImagesAsyncThrowsNotFoundForMissingTvShow()
    {
        var service = new GetTvShowImagesService(
            new FakeTvShowRepository(),
            new FakeImageProvider(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetImagesAsync(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "en"));
    }

    [Fact]
    public async Task GetImagesAsyncUsesLanguageSpecificCacheKey()
    {
        var tvShowId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var repository = new FakeTvShowRepository
        {
            TvShow = new TvShow
            {
                Id = tvShowId,
                TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
                Title = "Breaking Bad",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var cache = new InMemoryCacheService();
        var service = new GetTvShowImagesService(repository, new FakeImageProvider(), cache);

        await service.GetImagesAsync(tvShowId, "en");

        var cached = await cache.GetAsync<ImagesCacheEntry>(TvShowImagesCacheKeys.Create(tvShowId, "en"));
        Assert.NotNull(cached);
    }

    [Fact]
    public async Task GetImagesAsyncReturnsEmptyOnProviderFailureWithoutCaching()
    {
        var tvShowId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var repository = new FakeTvShowRepository
        {
            TvShow = new TvShow
            {
                Id = tvShowId,
                TmdbId = 42,
                Title = "Failure",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var provider = new ThrowingImageProvider();
        var cache = new InMemoryCacheService();
        var service = new GetTvShowImagesService(repository, provider, cache);

        var result = await service.GetImagesAsync(tvShowId, "en");

        Assert.Empty(result.Backdrops);
        Assert.Null(await cache.GetAsync<ImagesCacheEntry>(TvShowImagesCacheKeys.Create(tvShowId, "en")));
    }

    private sealed class FakeTvShowRepository : ITvShowRepository
    {
        public TvShow? TvShow { get; set; }

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(TvShow is not null && TvShow.Id == id ? TvShow : null);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(TvShowProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingImageProvider : IImageProvider
    {
        public Task<ProviderImagesResult?> GetMovieImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);

        public Task<ProviderImagesResult?> GetTvShowImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            throw new TmdbApiException(System.Net.HttpStatusCode.ServiceUnavailable, "TMDB unavailable.");

        public Task<ProviderImagesResult?> GetPersonImagesAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);
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
