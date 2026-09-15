using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Videos;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Videos;

public sealed class GetTvShowVideosServiceTests
{
    [Fact]
    public async Task GetVideosAsyncReturnsPrimaryTrailerForTvShow()
    {
        var tvShowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var repository = new FakeTvShowRepository
        {
            TvShow = new TvShow
            {
                Id = tvShowId,
                TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
                OriginalLanguage = "en",
                Title = "Breaking Bad",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var service = new GetTvShowVideosService(repository, new FakeVideoProvider(), new InMemoryCacheService());

        var result = await service.GetVideosAsync(tvShowId);

        Assert.NotNull(result.Primary);
        Assert.Equal("https://www.youtube.com/watch?v=fake-breaking-bad-trailer", result.Primary.WatchUrl);
    }

    [Fact]
    public async Task GetVideosAsyncReturnsNullPrimaryWhenNoTmdbId()
    {
        var tvShowId = Guid.Parse("22222222-2222-2222-2222-222222222222");
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
        var service = new GetTvShowVideosService(repository, new FakeVideoProvider(), new InMemoryCacheService());

        var result = await service.GetVideosAsync(tvShowId);

        Assert.Null(result.Primary);
    }

    [Fact]
    public async Task GetVideosAsyncThrowsNotFoundForMissingTvShow()
    {
        var service = new GetTvShowVideosService(
            new FakeTvShowRepository(),
            new FakeVideoProvider(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetVideosAsync(Guid.Parse("33333333-3333-3333-3333-333333333333")));
    }

    [Fact]
    public async Task GetVideosAsyncPropagatesProviderFailure()
    {
        var tvShowId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var repository = new FakeTvShowRepository
        {
            TvShow = new TvShow
            {
                Id = tvShowId,
                TmdbId = 42,
                OriginalLanguage = "en",
                Title = "Failure",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        var service = new GetTvShowVideosService(repository, new ThrowingVideoProvider(), new InMemoryCacheService());

        await Assert.ThrowsAsync<TmdbApiException>(() => service.GetVideosAsync(tvShowId));
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

    private sealed class ThrowingVideoProvider : IVideoProvider
    {
        public Task<IReadOnlyList<ProviderVideoResult>> GetMovieVideosAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderVideoResult>>([]);

        public Task<IReadOnlyList<ProviderVideoResult>> GetTvShowVideosAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new TmdbApiException(System.Net.HttpStatusCode.ServiceUnavailable, "TMDB unavailable.");
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : null);

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
