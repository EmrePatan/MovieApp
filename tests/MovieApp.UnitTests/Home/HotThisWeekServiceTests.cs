using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Home;

public sealed class HotThisWeekServiceTests
{
    [Fact]
    public void TmdbTrendingMapperExcludesPersonResults()
    {
        var person = TmdbTrendingMapper.ToProviderItem(new TmdbTrendingResultJson
        {
            Id = FakeTrendingWeekDataProvider.TrendingPersonTmdbId,
            MediaType = "person",
            Name = "Famous Person"
        });

        Assert.Null(person);
    }

    [Fact]
    public async Task GetItemsAsyncCachesTrendingResults()
    {
        var cache = new TrackingCacheService();
        var service = CreateService(cache, new FakeTrendingWeekDataProvider());

        var first = await service.GetItemsAsync(SearchContentType.All, 5);
        var second = await service.GetItemsAsync(SearchContentType.All, 5);

        Assert.Equal(3, first.Count);
        Assert.Equal(first.Select(item => item.Id), second.Select(item => item.Id));
        Assert.Equal(2, cache.GetCount);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task GetItemsAsyncReturnsEmptyWhenProviderFails()
    {
        var service = CreateService(new TrackingCacheService(), new ThrowingTrendingWeekDataProvider());

        var items = await service.GetItemsAsync(SearchContentType.All, 5);

        Assert.Empty(items);
    }

    private static HotThisWeekService CreateService(
        ICacheService cache,
        ITrendingWeekDataProvider provider) =>
        new(
            provider,
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            cache,
            Options.Create(new HomeOptions { HotThisWeekCacheTtlMinutes = 30 }),
            NullLogger<HotThisWeekService>.Instance);

    private sealed class TrackingCacheService : ICacheService
    {
        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            GetCount++;
            return Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) where T : class
        {
            SetCount++;
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingTrendingWeekDataProvider : ITrendingWeekDataProvider
    {
        public Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider unavailable");
    }

    private sealed class SummaryMovieRepository : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            var ids = summaries
                .Where(summary => summary.TmdbId.HasValue)
                .ToDictionary(
                    summary => summary.TmdbId!.Value,
                    summary => Guid.Parse($"11111111-1111-1111-1111-{summary.TmdbId!.Value:D012}"));

            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(ids);
        }
    }

    private sealed class SummaryTvShowRepository : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            var ids = summaries
                .Where(summary => summary.TmdbId.HasValue)
                .ToDictionary(
                    summary => summary.TmdbId!.Value,
                    summary => Guid.Parse($"22222222-2222-2222-2222-{summary.TmdbId!.Value:D012}"));

            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(ids);
        }
    }
}
