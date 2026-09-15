using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Credits;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.UnitTests.Credits;

public sealed class GetTvShowCreditsServiceTests
{
    [Fact]
    public async Task GetCreditsAsyncReturnsAggregateCastAndCrew()
    {
        var tvShowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
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
        var service = new GetTvShowCreditsService(repository, new FakeCreditsProvider(), new InMemoryCacheService());

        var result = await service.GetCreditsAsync(tvShowId);

        Assert.Equal(2, result.Cast.Count);
        Assert.Single(result.Crew);
        Assert.Equal(62, result.Cast[0].TotalEpisodeCount);
        Assert.NotNull(result.Cast[0].Roles);
    }

    [Fact]
    public async Task GetCreditsAsyncUsesV2CacheKey()
    {
        var tvShowId = Guid.Parse("22222222-2222-2222-2222-222222222222");
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
        var service = new GetTvShowCreditsService(repository, new FakeCreditsProvider(), cache);

        await service.GetCreditsAsync(tvShowId);

        var cached = await cache.GetAsync<CreditsCacheEntry>(TvShowCreditsCacheKeys.Create(tvShowId));
        Assert.NotNull(cached);
        Assert.Equal("v2", TvShowCreditsCacheKeys.Version);
        Assert.Contains(":v2", TvShowCreditsCacheKeys.Create(tvShowId), StringComparison.Ordinal);
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
