using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekServiceTests
{
    [Fact]
    public async Task GetOnTvThisWeekAsyncReturnsCachedResultsWithoutCallingCatalog()
    {
        var catalog = new RecordingOnTvThisWeekCatalog();
        var cache = new OnTvThisWeekFakeCache();
        var service = CreateService(catalog, cache);
        var criteria = new OnTvThisWeekCriteria(1, 20);
        var cachedResult = new PaginatedResult<SearchItem>(
            [CreateTvItem(Guid.NewGuid(), "Airing Drama")],
            1,
            20,
            1,
            1);

        await cache.SetAsync(
            OnTvThisWeekCacheKeys.Create(criteria),
            new DiscoveryCacheEntry { Result = cachedResult },
            TimeSpan.FromMinutes(30));

        var result = await service.GetOnTvThisWeekAsync(criteria);

        Assert.Single(result.Items);
        Assert.Equal(0, catalog.CallCount);
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncReturnsOnlyTvItems()
    {
        var service = CreateService(new RecordingOnTvThisWeekCatalog(), new OnTvThisWeekFakeCache());

        var result = await service.GetOnTvThisWeekAsync(new OnTvThisWeekCriteria(1, 20));

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Equal("tv", item.Type));
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncThrowsWhenCatalogFails()
    {
        var catalog = new RecordingOnTvThisWeekCatalog { ShouldThrow = true };
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(
            () => service.GetOnTvThisWeekAsync(new OnTvThisWeekCriteria(1, 20)));
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncDoesNotDependOnFollowedCatalogPath()
    {
        var catalog = new RecordingOnTvThisWeekCatalog();
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        await service.GetOnTvThisWeekAsync(new OnTvThisWeekCriteria(1, 20));

        Assert.Equal(1, catalog.CallCount);
    }

    private static OnTvThisWeekService CreateService(
        IOnTvThisWeekCatalog catalog,
        ICacheService cache) =>
        new(
            catalog,
            new FakeTvShowRepository(),
            cache,
            NullLogger<OnTvThisWeekService>.Instance);

    private static SearchItem CreateTvItem(Guid id, string title) =>
        new(id, "tv", title, null, null, null, null, null, 0m, 0, null);

    private sealed class RecordingOnTvThisWeekCatalog : IOnTvThisWeekCatalog
    {
        public int CallCount { get; private set; }

        public bool ShouldThrow { get; init; }

        public Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
            int page,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (ShouldThrow)
            {
                throw new InvalidOperationException("catalog failed");
            }

            return Task.FromResult(new TvShowProviderSearchResult(
                [
                    new TvShowProviderSummary(
                        "fake-tmdb-1",
                        1,
                        null,
                        "tt1",
                        "Airing Drama",
                        "Airing Drama",
                        "Overview",
                        new DateOnly(2024, 1, 1),
                        "/poster.jpg",
                        null,
                        "en",
                        7m,
                        10),
                ],
                page,
                20,
                1,
                1));
        }
    }

    private sealed class FakeTvShowRepository : ITvShowRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            var ids = summaries
                .Where(summary => summary.TmdbId is not null)
                .ToDictionary(
                    summary => summary.TmdbId!.Value,
                    summary => Guid.NewGuid());

            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(ids);
        }

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class OnTvThisWeekFakeCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (_entries.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
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
