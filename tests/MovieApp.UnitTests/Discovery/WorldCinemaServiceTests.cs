using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Providers.Tmdb;

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
            WorldCinemaCacheKeys.Create(criteria, ContentLocaleResolver.EnglishUnitedStates),
            new DiscoveryCacheEntry { Result = cachedResult },
            TimeSpan.FromMinutes(30));

        var result = await service.GetWorldCinemaAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.Equal(0, advancedDiscover.CallCount);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncMapsOriginCountryWithoutWatchOrReleaseRegion()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Movie, "kr", AdvancedDiscoverSort.PopularityDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        var mapped = advancedDiscover.LastCriteria;
        Assert.NotNull(mapped);
        Assert.Equal("KR", mapped.OriginCountry);
        Assert.Null(mapped.WatchRegion);
        Assert.Empty(mapped.WatchProviderIds);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncAppliesMovieVoteCountGuardrailForTopRatedSort()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Movie, "FR", AdvancedDiscoverSort.RatingDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(25, advancedDiscover.LastCriteria?.MinVoteCount);
        Assert.Equal(SearchContentType.Movie, advancedDiscover.LastCriteria?.MediaType);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncAppliesTvVoteCountGuardrailForTopRatedSort()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Tv, "FR", AdvancedDiscoverSort.RatingDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(20, advancedDiscover.LastCriteria?.MinVoteCount);
        Assert.Equal(SearchContentType.Tv, advancedDiscover.LastCriteria?.MediaType);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncDoesNotApplyVoteCountGuardrailForTvPopularitySort()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Tv, "IR", AdvancedDiscoverSort.PopularityDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Null(advancedDiscover.LastCriteria?.MinVoteCount);
    }

    [Theory]
    [InlineData(AdvancedDiscoverSort.PopularityDesc)]
    [InlineData(AdvancedDiscoverSort.Newest)]
    [InlineData(AdvancedDiscoverSort.Oldest)]
    public async Task GetWorldCinemaAsyncDoesNotApplyVoteCountGuardrailForNonTopRatedSorts(
        AdvancedDiscoverSort sort)
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", sort, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Null(advancedDiscover.LastCriteria?.MinVoteCount);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncPassesPaginationThroughToAdvancedDiscover()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService();
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await service.GetWorldCinemaAsync(
            new WorldCinemaCriteria(SearchContentType.Movie, "JP", AdvancedDiscoverSort.PopularityDesc, 3, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(3, advancedDiscover.LastCriteria?.Page);
        Assert.Equal(20, advancedDiscover.LastCriteria?.PageSize);
    }

    [Fact]
    public async Task GetWorldCinemaAsyncThrowsWhenAdvancedDiscoverFails()
    {
        var advancedDiscover = new RecordingAdvancedDiscoverService { ShouldThrow = true };
        var service = CreateService(advancedDiscover, new WorldCinemaFakeCache());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(
            () => service.GetWorldCinemaAsync(
                new WorldCinemaCriteria(SearchContentType.Movie, "JP", AdvancedDiscoverSort.PopularityDesc, 1, 20),
                ContentLocaleResolver.EnglishUnitedStates));
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
        Assert.Null(mapped.MinVoteCount);
    }

    [Theory]
    [InlineData(SearchContentType.Movie, AdvancedDiscoverSort.RatingDesc, 25)]
    [InlineData(SearchContentType.Tv, AdvancedDiscoverSort.RatingDesc, 20)]
    [InlineData(SearchContentType.Movie, AdvancedDiscoverSort.PopularityDesc, null)]
    [InlineData(SearchContentType.Tv, AdvancedDiscoverSort.Newest, null)]
    public void ResolveTopRatedMinimumVoteCountReturnsExpectedThreshold(
        SearchContentType mediaType,
        AdvancedDiscoverSort sort,
        int? expected)
    {
        Assert.Equal(expected, WorldCinemaService.ResolveTopRatedMinimumVoteCount(mediaType, sort));
    }

    [Fact]
    public void ToAdvancedDiscoverCriteriaIrMovieRatingDescPreservesOriginAndSort()
    {
        var mapped = WorldCinemaService.ToAdvancedDiscoverCriteria(
            new WorldCinemaCriteria(SearchContentType.Movie, "ir", AdvancedDiscoverSort.RatingDesc, 1, 20));

        Assert.Equal("IR", mapped.OriginCountry);
        Assert.Equal(25, mapped.MinVoteCount);
        Assert.Equal(AdvancedDiscoverSort.RatingDesc, mapped.Sort);
    }

    [Fact]
    public void ToAdvancedDiscoverCriteriaIrMovieRatingDescBuildsExpectedTmdbMovieQuery()
    {
        var mapped = WorldCinemaService.ToAdvancedDiscoverCriteria(
            new WorldCinemaCriteria(SearchContentType.Movie, "IR", AdvancedDiscoverSort.RatingDesc, 1, 20));
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(ToProviderCriteria(mapped));

        Assert.Contains("with_origin_country=IR", query);
        Assert.Contains("sort_by=vote_average.desc", query);
        Assert.Contains("vote_count.gte=25", query);
        Assert.Contains("include_adult=false", query);
    }

    [Fact]
    public void ToAdvancedDiscoverCriteriaIrTvRatingDescBuildsExpectedTmdbTvQuery()
    {
        var mapped = WorldCinemaService.ToAdvancedDiscoverCriteria(
            new WorldCinemaCriteria(SearchContentType.Tv, "IR", AdvancedDiscoverSort.RatingDesc, 1, 20));
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildTvQuery(ToProviderCriteria(mapped));

        Assert.Contains("with_origin_country=IR", query);
        Assert.Contains("sort_by=vote_average.desc", query);
        Assert.Contains("vote_count.gte=20", query);
        Assert.Contains("include_adult=false", query);
    }

    [Fact]
    public void AdvancedDiscoverRatingDescAloneDoesNotInjectWorldCinemaVoteThresholds()
    {
        var advancedDiscoverOnly = new AdvancedDiscoverCriteria(
            SearchContentType.Movie,
            [],
            GenreMatchMode.All,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "IR",
            null,
            null,
            [],
            null,
            [],
            [],
            [],
            [],
            AdvancedDiscoverSort.RatingDesc,
            1,
            20);

        Assert.Null(advancedDiscoverOnly.MinVoteCount);

        var fromWorldCinema = WorldCinemaService.ToAdvancedDiscoverCriteria(
            new WorldCinemaCriteria(SearchContentType.Movie, "IR", AdvancedDiscoverSort.RatingDesc, 1, 20));

        Assert.Equal(25, fromWorldCinema.MinVoteCount);
    }

    private static AdvancedDiscoverProviderCriteria ToProviderCriteria(AdvancedDiscoverCriteria criteria) =>
        new(
            criteria.Page,
            [],
            GenreMatchMode.All,
            null,
            null,
            null,
            null,
            null,
            criteria.MinVoteCount,
            null,
            null,
            null,
            criteria.OriginCountry,
            null,
            null,
            [],
            null,
            [],
            [],
            [],
            [],
            criteria.Sort);

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

        public Task<PaginatedResult<SearchItem>> DiscoverAsync(AdvancedDiscoverCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
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
