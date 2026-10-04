using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Home;

public sealed class TrendingWeekReleasePresentationTests
{
    private static readonly DateOnly Today = new(2026, 10, 4);

    [Fact]
    public async Task HotThisWeekSkipsFutureReleaseAndFillsHeroFromNextEligibleTitle()
    {
        List<SearchItem> weekly =
        [
            Item("A", Today.AddDays(-1)),
            Item("B", Today.AddDays(-2)),
            Item("Avengers Doomsday", Today.AddDays(60)),
            Item("D", Today),
        ];

        var hotThisWeek = CreateHotThisWeekService(new FakeSnapshotService(weekly));

        var hero = await hotThisWeek.GetItemsAsync(SearchContentType.All, 3, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["A", "B", "D"], hero.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task TrendingNowExcludesFutureTitlesAndDoesNotDuplicateHero()
    {
        List<SearchItem> weekly =
        [
            Item("A", Today.AddDays(-1)),
            Item("B", Today.AddDays(-2)),
            Item("Future", Today.AddDays(30)),
            Item("D", Today),
            Item("E", Today.AddDays(-3)),
            Item("F", Today.AddDays(-4)),
        ];

        var heroItems = weekly.Take(3).Where(item => item.Title != "Future").ToList();
        heroItems =
        [
            weekly[0],
            weekly[1],
            weekly[3],
        ];

        var service = CreateTrendingSectionService(new FakeSnapshotService(weekly));

        var trending = await service.GetTrendingItemsAsync(
            SearchContentType.All,
            heroItems,
            heroSize: 3,
            trendingSize: 2,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["E", "F"], trending.Select(item => item.Title).ToList());
        Assert.DoesNotContain(trending, item => heroItems.Any(hero => hero.Id == item.Id && hero.Type == item.Type));
    }

    [Fact]
    public async Task TrendingWeekListServiceExcludesFutureTitlesFromSeeAll()
    {
        var today = Today;
        List<SearchItem> providerItems =
        [
            Item("Released", today.AddDays(-1)),
            Item("Future", today.AddDays(10)),
        ];

        var listService = new TrendingWeekListService(
            new FakeTrendingWeekDataProvider(providerItems),
            new EmptyTrendingSnapshotService(),
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new NoOpCacheService(),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());

        var page = await listService.GetPageAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(page.Items);
        Assert.Equal("Released", page.Items[0].Title);
    }

    private static HotThisWeekService CreateHotThisWeekService(FakeSnapshotService snapshot) =>
        new(
            new RecordingTrendingWeekListService([]),
            snapshot,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            new NoOpCacheService(),
            new HotThisWeekLoadCoordinator(),
            Options.Create(new HomeOptions { HotThisWeekCacheTtlMinutes = 30 }),
            NullLogger<HotThisWeekService>.Instance);

    private static HomeWeeklyTrendingSectionService CreateTrendingSectionService(FakeSnapshotService snapshot) =>
        new(
            snapshot,
            new RecordingTrendingWeekListService([]),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());

    private static SearchItem Item(string title, DateOnly releaseDate) =>
        new(
            Guid.NewGuid(),
            "movie",
            title,
            null,
            null,
            "/poster.jpg",
            null,
            releaseDate,
            7m,
            100,
            releaseDate.Year);

    private sealed class FakeSnapshotService(IReadOnlyList<SearchItem> items) : IHotThisWeekTrendingSnapshotService
    {
        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(new HotThisWeekTrendingSnapshotEntry
            {
                RefreshedAt = DateTimeOffset.UtcNow,
                Items = items,
            });

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTrendingWeekListService(IReadOnlyList<SearchItem> items) : ITrendingWeekListService
    {
        public Task<PaginatedResult<SearchItem>> GetPageAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>(
                items.Take(criteria.PageSize).ToList(),
                criteria.Page,
                criteria.PageSize,
                items.Count,
                1));
    }

    private sealed class FakeTrendingWeekDataProvider(IReadOnlyList<SearchItem> items)
        : MovieApp.Application.Abstractions.Providers.ITrendingWeekDataProvider
    {
        public Task<IReadOnlyList<MovieApp.Application.Models.Providers.TrendingWeekProviderItem>> GetTrendingWeekAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Application.Models.Providers.TrendingWeekPage> GetTrendingWeekPageAsync(
            int page,
            CancellationToken cancellationToken = default)
        {
            var providerItems = items.Select(item => new MovieApp.Application.Models.Providers.TrendingWeekProviderItem(
                item.Type,
                item.TmdbId ?? 1,
                item.Title,
                item.OriginalTitle,
                item.Overview,
                item.ReleaseDate,
                item.PosterUrl,
                item.BackdropUrl,
                item.VoteAverage,
                item.VoteCount)).ToList();

            return Task.FromResult(new MovieApp.Application.Models.Providers.TrendingWeekPage(
                providerItems,
                page,
                providerItems.Count,
                1));
        }
    }

    private sealed class NoOpMovieRepository : MovieApp.Application.Abstractions.Persistence.IMovieRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieApp.Application.Models.Providers.MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries.ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));

        public Task<MovieApp.Domain.Entities.Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie> UpsertFromProviderAsync(
            MovieApp.Application.Models.Providers.MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpTvShowRepository : MovieApp.Application.Abstractions.Persistence.ITvShowRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieApp.Application.Models.Providers.TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<MovieApp.Domain.Entities.TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow> UpsertFromProviderAsync(
            MovieApp.Application.Models.Providers.TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyTrendingSnapshotService : IHotThisWeekTrendingSnapshotService
    {
        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(null);

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpCacheService : MovieApp.Application.Abstractions.Caching.ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
