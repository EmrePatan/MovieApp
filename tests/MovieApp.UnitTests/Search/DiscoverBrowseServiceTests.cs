using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverBrowseServiceTests
{
    [Fact]
    public async Task BrowseAsyncReturnsCachedResultWithoutCallingProviders()
    {
        var cachedItem = CreateSearchItem("movie", Guid.NewGuid());
        var cache = new DiscoverBrowseFakeCacheService(
            new PaginatedResult<SearchItem>([cachedItem], 1, 20, 1, 1));
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.Movie));

        Assert.Single(result.Items);
        Assert.Equal(cachedItem.Id, result.Items[0].Id);
        Assert.Equal(0, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal(0, tvTracker.DiscoverTvShowsCallCount);
    }

    [Fact]
    public async Task BrowseAsyncUsesOnlyMovieProviderForMovieType()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.Movie));

        Assert.NotEmpty(result.Items);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal(0, tvTracker.DiscoverTvShowsCallCount);
        Assert.All(result.Items, item => Assert.Equal("movie", item.Type));
    }

    [Fact]
    public async Task BrowseAsyncUsesBothProvidersForAllType()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.All));

        Assert.NotEmpty(result.Items);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
        Assert.Equal(1, tvTracker.DiscoverTvShowsCallCount);
        Assert.Contains(result.Items, item => item.Type == "movie");
        Assert.Contains(result.Items, item => item.Type == "tv");
    }

    [Fact]
    public async Task BrowseAsyncMaterializesSummariesWithoutDetailCalls()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var movieRepository = new SummaryMovieRepository();
        var tvRepository = new SummaryTvShowRepository();
        var service = new DiscoverBrowseService(
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(tvTracker),
            movieRepository,
            tvRepository,
            new FakeGenreReadRepository(),
            cache,
            NullLogger<DiscoverBrowseService>.Instance);

        var result = await service.BrowseAsync(CreateCriteria(SearchContentType.All));

        Assert.True(movieRepository.EnsureCount > 0);
        Assert.True(tvRepository.EnsureCount > 0);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
    }

    [Fact]
    public async Task BrowseAsyncThrowsWhenRequiredProviderFails()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker { FailDiscoverMovies = true };
        var service = CreateService(
            cache,
            movieTracker,
            new TvShowDataProviderCallTracker());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.BrowseAsync(CreateCriteria(SearchContentType.Movie)));
    }

    [Fact]
    public async Task BrowseAsyncNewReleasesExcludesFutureDatedFakeCatalogItems()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var service = CreateService(
            cache,
            new MovieDataProviderCallTracker(),
            new TvShowDataProviderCallTracker());

        var result = await service.BrowseAsync(CreateCriteria(
            SearchContentType.Movie,
            DiscoverBrowseMode.NewReleases));

        Assert.DoesNotContain(result.Items, item => item.Title == "Discover Movie Future");
    }

    [Fact]
    public async Task BrowseAsyncCachesSuccessfulResponses()
    {
        var cache = new DiscoverBrowseFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var service = CreateService(
            cache,
            movieTracker,
            new TvShowDataProviderCallTracker());
        var criteria = CreateCriteria(SearchContentType.Movie);

        await service.BrowseAsync(criteria);
        await service.BrowseAsync(criteria);

        Assert.Equal(1, cache.SetCount);
        Assert.Equal(2, cache.GetCount);
        Assert.Equal(1, movieTracker.DiscoverMoviesCallCount);
    }

    private static DiscoverBrowseService CreateService(
        DiscoverBrowseFakeCacheService cache,
        MovieDataProviderCallTracker movieTracker,
        TvShowDataProviderCallTracker tvTracker) =>
        new(
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(tvTracker),
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            new FakeGenreReadRepository(),
            cache,
            NullLogger<DiscoverBrowseService>.Instance);

    private static DiscoverBrowseCriteria CreateCriteria(
        SearchContentType type,
        DiscoverBrowseMode mode = DiscoverBrowseMode.Trending) =>
        new(
            mode,
            type,
            [],
            null,
            null,
            null,
            null,
            1,
            20);

    private static SearchItem CreateSearchItem(string type, Guid id) =>
        new(
            id,
            type,
            "Title",
            null,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(2024, 1, 1),
            8.0m,
            100,
            2024);

    private sealed class DiscoverBrowseFakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();
        private readonly PaginatedResult<SearchItem>? _seededResult;

        public DiscoverBrowseFakeCacheService(PaginatedResult<SearchItem>? seededResult)
        {
            _seededResult = seededResult;
        }

        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            GetCount++;

            if (_seededResult is not null &&
                typeof(T) == typeof(DiscoveryCacheEntry) &&
                GetCount == 1)
            {
                return Task.FromResult(new DiscoveryCacheEntry { Result = _seededResult } as T);
            }

            if (_entries.TryGetValue(key, out var value) && value is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
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
            SetCount++;
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SummaryMovieRepository : MovieApp.Application.Abstractions.Persistence.IMovieRepository
    {
        public int EnsureCount { get; private set; }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            EnsureCount++;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(
                        summary => summary.TmdbId!.Value,
                        summary => Guid.NewGuid()));
        }

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SummaryTvShowRepository : MovieApp.Application.Abstractions.Persistence.ITvShowRepository
    {
        public int EnsureCount { get; private set; }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            EnsureCount++;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(
                        summary => summary.TmdbId!.Value,
                        summary => Guid.NewGuid()));
        }

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeGenreReadRepository : MovieApp.Application.Abstractions.Persistence.IGenreReadRepository
    {
        public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]);

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
            IReadOnlyList<Guid> genreIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
