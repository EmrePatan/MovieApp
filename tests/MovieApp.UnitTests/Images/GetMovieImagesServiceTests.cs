using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Images;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Images;

public sealed class GetMovieImagesServiceTests
{
    [Fact]
    public async Task GetImagesAsyncReturnsOrderedGalleryForMovie()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = FakeMovieDataProvider.InterstellarTmdbId,
                Title = "Interstellar",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var service = new GetMovieImagesService(repository, new FakeImageProvider(), new InMemoryCacheService());

        var result = await service.GetImagesAsync(movieId, "en");

        Assert.Equal(2, result.Backdrops.Count);
        Assert.Equal("/fake/interstellar-backdrop-en.jpg", result.Backdrops[0].FilePath);
        Assert.Equal(2, result.Posters.Count);
        Assert.Single(result.Logos);
    }

    [Fact]
    public async Task GetImagesAsyncReturnsEmptyWhenNoTmdbId()
    {
        var movieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = null,
                Title = "No Provider",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var service = new GetMovieImagesService(repository, new FakeImageProvider(), new InMemoryCacheService());

        var result = await service.GetImagesAsync(movieId, "en");

        Assert.Empty(result.Backdrops);
        Assert.Empty(result.Posters);
        Assert.Empty(result.Logos);
    }

    [Fact]
    public async Task GetImagesAsyncThrowsNotFoundForMissingMovie()
    {
        var service = new GetMovieImagesService(
            new FakeMovieRepository(),
            new FakeImageProvider(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetImagesAsync(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "en"));
    }

    [Fact]
    public async Task GetImagesAsyncUsesCacheAndSkipsProvider()
    {
        var movieId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = FakeMovieDataProvider.InterstellarTmdbId,
                Title = "Interstellar",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var provider = new CountingImageProvider();
        var cache = new InMemoryCacheService();
        var service = new GetMovieImagesService(repository, provider, cache);

        await service.GetImagesAsync(movieId, "en");
        await service.GetImagesAsync(movieId, "en");

        Assert.Equal(1, provider.MovieCallCount);
    }

    [Fact]
    public async Task GetImagesAsyncReturnsEmptyOnProviderFailureWithoutCaching()
    {
        var movieId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = 42,
                Title = "Failure",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var provider = new ThrowingImageProvider();
        var cache = new InMemoryCacheService();
        var service = new GetMovieImagesService(repository, provider, cache);

        var firstResult = await service.GetImagesAsync(movieId, "en");

        Assert.Empty(firstResult.Backdrops);

        provider.ShouldThrow = false;
        provider.MovieImages = FakeImageProvider.InterstellarImages;
        var secondResult = await service.GetImagesAsync(movieId, "en");

        Assert.NotEmpty(secondResult.Backdrops);
        Assert.Equal(2, provider.MovieCallCount);

        var cached = await cache.GetAsync<ImagesCacheEntry>(MovieImagesCacheKeys.Create(movieId, "en"));
        Assert.NotNull(cached);
    }

    private sealed class FakeMovieRepository : IMovieRepository
    {
        public Movie? Movie { get; set; }

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Movie is not null && Movie.Id == id ? Movie : null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingImageProvider : IImageProvider
    {
        public int MovieCallCount { get; private set; }

        public Task<ProviderImagesResult?> GetMovieImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default)
        {
            MovieCallCount += 1;
            return Task.FromResult<ProviderImagesResult?>(FakeImageProvider.InterstellarImages);
        }

        public Task<ProviderImagesResult?> GetTvShowImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);

        public Task<ProviderImagesResult?> GetPersonImagesAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);
    }

    private sealed class ThrowingImageProvider : IImageProvider
    {
        public bool ShouldThrow { get; set; } = true;

        public int MovieCallCount { get; private set; }

        public ProviderImagesResult? MovieImages { get; set; }

        public Task<ProviderImagesResult?> GetMovieImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default)
        {
            MovieCallCount += 1;
            if (ShouldThrow)
            {
                throw new TmdbApiException(System.Net.HttpStatusCode.ServiceUnavailable, "TMDB unavailable.");
            }

            return Task.FromResult(MovieImages);
        }

        public Task<ProviderImagesResult?> GetTvShowImagesAsync(int tmdbId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderImagesResult?>(null);

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
