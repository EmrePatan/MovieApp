using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class WorldCinemaServiceTests
{
    [Fact]
    public async Task GetWorldCinemaAsyncReturnsCachedResultsWithoutCallingAdvancedDiscover()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var cache = new WorldCinemaFakeCache();
        var service = CreateService(advancedDiscover, cache);
        var criteria = new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 20);
        var cachedResult = new PaginatedResult<SearchItem>(
            [CreateMovieItem(Guid.NewGuid(), "Parasite")],
            1,
            20,
            1,
            1);

        await cache.SetAsync(
            WorldCinemaCacheKeys.Create(criteria),
            new DiscoveryCacheEntry { Result = cachedResult },
            TimeSpan.FromMinutes(30));

        var result = await service.GetWorldCinemaAsync(criteria);

        Assert.Single(result.Items);
        Assert.Equal(0, advancedDiscover.CallCount);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncMapsOriginCountryWithoutWatchOrReleaseRegion()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Movie, "kr", AdvancedDiscoverSort.PopularityDesc, 1, 20));

        var mapped = advancedDiscover.LastCriteria;
        Assert.NotNull(mapped);
        Assert.Equal("KR", mapped.OriginCountry);
        Assert.Null(mapped.WatchRegion);
        Assert.Empty(mapped.WatchProviderIds);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncAppliesVoteCountGuardrailForTopRatedSort()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Tv, "FR", AdvancedDiscoverSort.RatingDesc, 1, 20));

        Assert.Equal(50, advancedDiscover.LastCriteria?.MinVoteCount);
        Assert.Equal(SearchContentType.Tv, advancedDiscover.LastCriteria?.MediaType);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncThrowsWhenAdvancedDiscoverFails()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService { ShouldThrow = true };
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(
            () => service.GetWorldCinemaAsync(
                new WorldCinemaCriteria(SearchContentType.Movie, "JP", AdvancedDiscoverSort.PopularityDesc, 1, 20)));
    }

    [Fact]
    public void ToAdvancedDiscoverCriteriaPreservesD1OriginCountrySemantics()
    {
        var mapped = WorldCinemaService.ToAdvancedDiscoverCriteria(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.PopularityDesc, 2, 20));

        Assert.Equal("KR", mapped.OriginCountry);
        Assert.Null(mapped.WatchRegion);
        Assert.Null(mapped.OriginalLanguage);
        Assert.Equal(AdvancedDiscoverSort.PopularityDesc, mapped.Sort);
    }

    private static WorldCinemaService CreateService(
        IAdvancedDiscoverService advancedDiscover,
        ICacheService cache) =>
        new(advancedDiscover, cache);

    private static SearchItem CreateMovieItem(Guid id, string title) =>
        new(id, "movie", title, null, null, "/poster.jpg", null, null, 0m, 0, null);

    private sealed class RecordingAdvancedDiscoverService : IAdvancedDiscoverService
    {
        public int CallCount { get; private set; }

        public AdvancedDiscoverCriteria? LastCriteria { get; private set; }

        public bool ShouldThrow { get; init; }

        public Task<PaginatedResult<SearchItem>> DiscoverAsync(
            AdvancedDiscoverCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCriteria = criteria;

            if (ShouldThrow)
            {
                throw new SearchProviderUnavailableException();
            }

            var item = criteria.MediaType == SearchContentType.Tv
                ? new SearchItem(Guid.NewGuid(), "tv", "Global Drama", null, null, "/poster.jpg", null, null, 0m, 0, null)
                : CreateMovieItem(Guid.NewGuid(), "Global Film");

            return Task.FromResult(new PaginatedResult<SearchItem>(
                [item],
                criteria.Page,
                criteria.PageSize,
                1,
                1));
        }
    }

    private sealed class WorldCinemaFakeCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (_entries.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult<T?>(default);
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
