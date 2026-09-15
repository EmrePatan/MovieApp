using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Videos;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Videos;

public sealed class GetMovieVideosServiceTests
{
    [Fact]
    public async Task GetVideosAsyncReturnsPrimaryTrailerForMovie()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = FakeMovieDataProvider.InterstellarTmdbId,
                OriginalLanguage = "en",
                Title = "Interstellar",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var cache = new InMemoryCacheService();
        var service = new GetMovieVideosService(repository, new FakeVideoProvider(), cache);

        var result = await service.GetVideosAsync(movieId);

        Assert.NotNull(result.Primary);
        Assert.Equal("Trailer", result.Primary.Type);
        Assert.Equal("https://www.youtube.com/watch?v=fake-interstellar-trailer", result.Primary.WatchUrl);
    }

    [Fact]
    public async Task GetVideosAsyncReturnsNullPrimaryWhenNoTmdbId()
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
        var service = new GetMovieVideosService(repository, new FakeVideoProvider(), new InMemoryCacheService());

        var result = await service.GetVideosAsync(movieId);

        Assert.Null(result.Primary);
    }

    [Fact]
    public async Task GetVideosAsyncReturnsNullPrimaryWhenNoEligibleVideo()
    {
        var movieId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = 123456,
                OriginalLanguage = "en",
                Title = "No Videos",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var service = new GetMovieVideosService(repository, new FakeVideoProvider(), new InMemoryCacheService());

        var result = await service.GetVideosAsync(movieId);

        Assert.Null(result.Primary);
    }

    [Fact]
    public async Task GetVideosAsyncThrowsNotFoundForMissingMovie()
    {
        var service = new GetMovieVideosService(
            new FakeMovieRepository(),
            new FakeVideoProvider(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetVideosAsync(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")));
    }

    [Fact]
    public async Task GetVideosAsyncUsesCacheAndSkipsProvider()
    {
        var movieId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = FakeMovieDataProvider.InterstellarTmdbId,
                OriginalLanguage = "en",
                Title = "Interstellar",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var provider = new CountingVideoProvider();
        var cache = new InMemoryCacheService();
        var service = new GetMovieVideosService(repository, provider, cache);

        await service.GetVideosAsync(movieId);
        await service.GetVideosAsync(movieId);

        Assert.Equal(1, provider.MovieCallCount);
    }

    [Fact]
    public async Task GetVideosAsyncDoesNotCacheProviderFailure()
    {
        var movieId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var repository = new FakeMovieRepository
        {
            Movie = new Movie
            {
                Id = movieId,
                TmdbId = 42,
                OriginalLanguage = "en",
                Title = "Failure",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var provider = new ThrowingVideoProvider();
        var cache = new InMemoryCacheService();
        var service = new GetMovieVideosService(repository, provider, cache);

        await Assert.ThrowsAsync<TmdbApiException>(() => service.GetVideosAsync(movieId));

        provider.ShouldThrow = false;
        provider.MovieVideos = FakeVideoProvider.InterstellarVideos;
        var result = await service.GetVideosAsync(movieId);

        Assert.NotNull(result.Primary);
        Assert.Equal(2, provider.MovieCallCount);
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

    private sealed class CountingVideoProvider : IVideoProvider
    {
        public int MovieCallCount { get; private set; }

        public Task<IReadOnlyList<ProviderVideoResult>> GetMovieVideosAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            MovieCallCount += 1;
            return Task.FromResult<IReadOnlyList<ProviderVideoResult>>(FakeVideoProvider.InterstellarVideos);
        }

        public Task<IReadOnlyList<ProviderVideoResult>> GetTvShowVideosAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderVideoResult>>([]);
    }

    private sealed class ThrowingVideoProvider : IVideoProvider
    {
        public bool ShouldThrow { get; set; } = true;

        public int MovieCallCount { get; private set; }

        public IReadOnlyList<ProviderVideoResult> MovieVideos { get; set; } = [];

        public Task<IReadOnlyList<ProviderVideoResult>> GetMovieVideosAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            MovieCallCount += 1;
            if (ShouldThrow)
            {
                throw new TmdbApiException(System.Net.HttpStatusCode.ServiceUnavailable, "TMDB unavailable.");
            }

            return Task.FromResult(MovieVideos);
        }

        public Task<IReadOnlyList<ProviderVideoResult>> GetTvShowVideosAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderVideoResult>>([]);
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
