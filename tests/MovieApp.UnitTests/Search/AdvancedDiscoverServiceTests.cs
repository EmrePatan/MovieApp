using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Search;

public sealed class AdvancedDiscoverServiceTests
{
    [Fact]
    public async Task DiscoverAsyncReturnsCachedResultWithoutCallingProviders()
    {
        var cachedItem = CreateSearchItem("movie", Guid.NewGuid());
        var cache = new AdvancedDiscoverFakeCacheService(
            new PaginatedResult<SearchItem>([cachedItem], 1, 20, 1, 1));
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.DiscoverAsync(CreateCriteria(SearchContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.Equal(cachedItem.Id, result.Items[0].Id);
        Assert.Equal(0, movieTracker.AdvancedDiscoverMoviesCallCount);
        Assert.Equal(0, tvTracker.AdvancedDiscoverTvShowsCallCount);
    }

    [Fact]
    public async Task DiscoverAsyncUsesMovieProviderForMovieType()
    {
        var cache = new AdvancedDiscoverFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.DiscoverAsync(CreateCriteria(SearchContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEmpty(result.Items);
        Assert.Equal(1, movieTracker.AdvancedDiscoverMoviesCallCount);
        Assert.Equal(0, tvTracker.AdvancedDiscoverTvShowsCallCount);
        Assert.All(result.Items, item => Assert.Equal("movie", item.Type));
    }

    [Fact]
    public async Task DiscoverAsyncUsesTvProviderForTvType()
    {
        var cache = new AdvancedDiscoverFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var tvTracker = new TvShowDataProviderCallTracker();
        var service = CreateService(cache, movieTracker, tvTracker);

        var result = await service.DiscoverAsync(CreateCriteria(SearchContentType.Tv), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEmpty(result.Items);
        Assert.Equal(0, movieTracker.AdvancedDiscoverMoviesCallCount);
        Assert.Equal(1, tvTracker.AdvancedDiscoverTvShowsCallCount);
        Assert.All(result.Items, item => Assert.Equal("tv", item.Type));
    }

    [Fact]
    public async Task DiscoverAsyncMaterializesSummariesForDetailNavigation()
    {
        var cache = new AdvancedDiscoverFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker();
        var movieRepository = new SummaryMovieRepository();
        var service = new AdvancedDiscoverService(
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(new TvShowDataProviderCallTracker()),
            new SearchTestDoubles.FakeLocalizedListDataProvider(),
            movieRepository,
            new SummaryTvShowRepository(),
            new FakeGenreReadRepository(),
            cache,
            NullLogger<AdvancedDiscoverService>.Instance);

        var result = await service.DiscoverAsync(CreateCriteria(SearchContentType.Movie), ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(movieRepository.EnsureCount > 0);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
    }

    [Fact]
    public async Task DiscoverAsyncThrowsWhenProviderFails()
    {
        var cache = new AdvancedDiscoverFakeCacheService(null);
        var movieTracker = new MovieDataProviderCallTracker { FailAdvancedDiscoverMovies = true };
        var service = CreateService(
            cache,
            movieTracker,
            new TvShowDataProviderCallTracker());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.DiscoverAsync(CreateCriteria(SearchContentType.Movie), ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task DiscoverAsyncPropagatesCallerCancellationInsteadOfReportingProviderOutage()
    {
        var service = CreateService(
            new AdvancedDiscoverFakeCacheService(null),
            new MovieDataProviderCallTracker(),
            new TvShowDataProviderCallTracker());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DiscoverAsync(
                CreateCriteria(SearchContentType.Movie),
                ContentLocaleResolver.EnglishUnitedStates,
                cancellation.Token));

        Assert.IsNotType<SearchProviderUnavailableException>(exception);
    }

    [Fact]
    public async Task DiscoverAsyncRejectsAllMediaType()
    {
        var service = CreateService(
            new AdvancedDiscoverFakeCacheService(null),
            new MovieDataProviderCallTracker(),
            new TvShowDataProviderCallTracker());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.DiscoverAsync(CreateCriteria(SearchContentType.All), ContentLocaleResolver.EnglishUnitedStates));
    }

    private static AdvancedDiscoverService CreateService(
        AdvancedDiscoverFakeCacheService cache,
        MovieDataProviderCallTracker movieTracker,
        TvShowDataProviderCallTracker tvTracker) =>
        new(
            new FakeMovieDataProvider(movieTracker),
            new FakeTvShowDataProvider(tvTracker),
            new SearchTestDoubles.FakeLocalizedListDataProvider(),
            new SummaryMovieRepository(),
            new SummaryTvShowRepository(),
            new FakeGenreReadRepository(),
            cache,
            NullLogger<AdvancedDiscoverService>.Instance);

    private static AdvancedDiscoverCriteria CreateCriteria(SearchContentType type) =>
        new(
            type,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            AdvancedDiscoverSort.PopularityDesc,
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

    private sealed class AdvancedDiscoverFakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();
        private readonly PaginatedResult<SearchItem>? _seededResult;

        public AdvancedDiscoverFakeCacheService(PaginatedResult<SearchItem>? seededResult)
        {
            _seededResult = seededResult;
        }

        public int GetCount { get; private set; }

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
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(
                        summary => summary.TmdbId!.Value,
                        summary => Guid.NewGuid()));

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

        public Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);
    }
}
