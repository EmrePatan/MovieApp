using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Credits;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Credits;

public sealed class GetMovieCreditsServiceTests
{
    [Fact]
    public async Task GetCreditsAsyncReturnsFullCastAndCrewWithoutTrimming()
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
        var provider = new LargeCastCreditsProvider();
        var service = new GetMovieCreditsService(repository, provider, new InMemoryCacheService());

        var result = await service.GetCreditsAsync(movieId);

        Assert.Equal(15, result.Cast.Count);
        Assert.Equal(2, result.Crew.Count);
    }

    [Fact]
    public async Task GetCreditsAsyncReturnsEmptyCreditsWhenNoTmdbId()
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
        var service = new GetMovieCreditsService(repository, new FakeCreditsProvider(), new InMemoryCacheService());

        var result = await service.GetCreditsAsync(movieId);

        Assert.Empty(result.Cast);
        Assert.Empty(result.Crew);
    }

    [Fact]
    public async Task GetCreditsAsyncThrowsNotFoundForMissingMovie()
    {
        var service = new GetMovieCreditsService(
            new FakeMovieRepository(),
            new FakeCreditsProvider(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetCreditsAsync(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));
    }

    [Fact]
    public async Task GetCreditsAsyncUsesCacheAndSkipsProvider()
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
        var provider = new CountingCreditsProvider();
        var cache = new InMemoryCacheService();
        var service = new GetMovieCreditsService(repository, provider, cache);

        await service.GetCreditsAsync(movieId);
        await service.GetCreditsAsync(movieId);

        Assert.Equal(1, provider.MovieCallCount);
    }

    [Fact]
    public async Task GetCreditsAsyncCachesFullPayloadIncludingCrew()
    {
        var movieId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
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
        var cache = new InMemoryCacheService();
        var service = new GetMovieCreditsService(repository, new FakeCreditsProvider(), cache);

        await service.GetCreditsAsync(movieId);

        var cached = await cache.GetAsync<CreditsCacheEntry>(MovieCreditsCacheKeys.Create(movieId));
        Assert.NotNull(cached);
        Assert.Equal(3, cached!.Result.Cast.Count);
        Assert.Equal(3, cached.Result.Crew.Count);
    }

    [Fact]
    public async Task GetCreditsAsyncDoesNotCacheProviderFailure()
    {
        var movieId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
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
        var provider = new ThrowingCreditsProvider();
        var cache = new InMemoryCacheService();
        var service = new GetMovieCreditsService(repository, provider, cache);

        await Assert.ThrowsAsync<TmdbApiException>(() => service.GetCreditsAsync(movieId));

        provider.ShouldThrow = false;
        var result = await service.GetCreditsAsync(movieId);

        Assert.NotEmpty(result.Cast);
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

    private sealed class CountingCreditsProvider : ICreditsProvider
    {
        public int MovieCallCount { get; private set; }

        public Task<CreditsResult> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            MovieCallCount += 1;
            return Task.FromResult(new CreditsResult(FakeCreditsProvider.InterstellarCast, FakeCreditsProvider.InterstellarCrew));
        }

        public Task<CreditsResult> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreditsResult([], []));
    }

    private sealed class LargeCastCreditsProvider : ICreditsProvider
    {
        public Task<CreditsResult> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            var cast = Enumerable.Range(0, 15)
                .Select(index => new CastMemberResult(index + 1, $"Actor {index}", $"Role {index}", null, index))
                .ToList();
            var crew = new List<CrewMemberResult>
            {
                new(9001, "Director", "Directing", ["Director"], null),
                new(9002, "Composer", "Sound", ["Composer"], null),
            };

            return Task.FromResult(new CreditsResult(cast, crew));
        }

        public Task<CreditsResult> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreditsResult([], []));
    }

    private sealed class ThrowingCreditsProvider : ICreditsProvider
    {
        public bool ShouldThrow { get; set; } = true;

        public int MovieCallCount { get; private set; }

        public Task<CreditsResult> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            MovieCallCount += 1;
            if (ShouldThrow)
            {
                throw new TmdbApiException(System.Net.HttpStatusCode.ServiceUnavailable, "TMDB unavailable.");
            }

            return Task.FromResult(new CreditsResult(FakeCreditsProvider.InterstellarCast, FakeCreditsProvider.InterstellarCrew));
        }

        public Task<CreditsResult> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreditsResult([], []));
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
