using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Application.Services.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class NowInTheatersServiceTests
{
    [Fact]
    public async Task GetNowInTheatersAsyncReturnsCachedResultsWithoutCallingCatalog()
    {
        var catalog = new RecordingNowInTheatersMovieCatalog();
        var cache = new NowInTheatersFakeCache();
        var service = CreateService(catalog, cache);
        var criteria = new NowInTheatersCriteria("TR", 1, 20);
        var cachedResult = new PaginatedResult<SearchItem>(
            [CreateMovieItem(Guid.NewGuid(), "Cinema One")],
            1,
            20,
            1,
            1);

        await cache.SetAsync(
            NowInTheatersCacheKeys.Create(criteria),
            new DiscoveryCacheEntry { Result = cachedResult },
            TimeSpan.FromMinutes(30));

        var result = await service.GetNowInTheatersAsync(criteria);

        Assert.Single(result.Items);
        Assert.Equal(0, catalog.CallCount);
    }

    [Fact]
    public async Task GetNowInTheatersAsyncUsesReleaseRegionWhenFetchingCatalog()
    {
        var catalog = new RecordingNowInTheatersMovieCatalog();
        var service = CreateService(catalog, new NowInTheatersFakeCache());

        await service.GetNowInTheatersAsync(new NowInTheatersCriteria("us", 1, 20));

        Assert.Equal(1, catalog.CallCount);
        Assert.Equal("US", catalog.LastReleaseRegion);
        Assert.Equal(1, catalog.LastPage);
    }

    [Fact]
    public async Task GetNowInTheatersAsyncReturnsOnlyMovieItems()
    {
        var catalog = new RecordingNowInTheatersMovieCatalog();
        var service = CreateService(catalog, new NowInTheatersFakeCache());

        var result = await service.GetNowInTheatersAsync(new NowInTheatersCriteria("TR", 1, 20));

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Equal("movie", item.Type));
    }

    [Fact]
    public async Task GetNowInTheatersAsyncThrowsWhenCatalogFails()
    {
        var catalog = new RecordingNowInTheatersMovieCatalog { ShouldThrow = true };
        var service = CreateService(catalog, new NowInTheatersFakeCache());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(
            () => service.GetNowInTheatersAsync(new NowInTheatersCriteria("TR", 1, 20)));
    }

    [Fact]
    public async Task GetNowInTheatersAsyncReturnsEmptyResultForUnknownRegion()
    {
        var catalog = new RecordingNowInTheatersMovieCatalog();
        var service = CreateService(catalog, new NowInTheatersFakeCache());

        var result = await service.GetNowInTheatersAsync(new NowInTheatersCriteria("GB", 1, 20));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private static NowInTheatersService CreateService(
        INowInTheatersMovieCatalog catalog,
        ICacheService cache) =>
        new(
            catalog,
            new FakeMovieRepository(),
            cache,
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }),
            NullLogger<NowInTheatersService>.Instance);

    private static SearchItem CreateMovieItem(Guid id, string title) =>
        new(id, "movie", title, null, null, null, null, null, 0m, 0, null);

    private sealed class RecordingNowInTheatersMovieCatalog : INowInTheatersMovieCatalog
    {
        public int CallCount { get; private set; }

        public string LastReleaseRegion { get; private set; } = string.Empty;

        public int LastPage { get; private set; }

        public bool ShouldThrow { get; init; }

        public Task<MovieProviderSearchResult> GetNowPlayingMoviesAsync(
            string releaseRegion,
            int page,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastReleaseRegion = releaseRegion;
            LastPage = page;

            if (ShouldThrow)
            {
                throw new InvalidOperationException("catalog failed");
            }

            IReadOnlyList<MovieProviderSummary> summaries;
            if (releaseRegion.Equals("US", StringComparison.OrdinalIgnoreCase))
            {
                summaries =
                [
                    new MovieProviderSummary(
                        "fake-tmdb-1",
                        1,
                        null,
                        "tt1",
                        "US Movie",
                        "Overview",
                        new DateOnly(2026, 1, 1),
                        "/poster.jpg",
                        7m,
                        10),
                ];
            }
            else if (releaseRegion.Equals("TR", StringComparison.OrdinalIgnoreCase))
            {
                summaries =
                [
                    new MovieProviderSummary(
                        "fake-tmdb-2",
                        2,
                        null,
                        "tt2",
                        "Cinema One",
                        "Overview",
                        new DateOnly(2026, 1, 1),
                        "/poster.jpg",
                        7m,
                        10),
                ];
            }
            else
            {
                summaries = [];
            }

            return Task.FromResult(new MovieProviderSearchResult(
                summaries,
                page,
                20,
                summaries.Count,
                summaries.Count == 0 ? 0 : 1));
        }
    }

    private sealed class FakeMovieRepository : IMovieRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            var ids = summaries
                .Where(summary => summary.TmdbId is not null)
                .ToDictionary(
                    summary => summary.TmdbId!.Value,
                    summary => Guid.NewGuid());

            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(ids);
        }

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NowInTheatersFakeCache : ICacheService
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
