using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.UnitTests.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Home;

public sealed class HomeWeeklyTrendingCompositionServiceTests
{
    [Fact]
    public async Task ComposeAsyncUsesWeeklySnapshotHeadForHeroAndTailForTrending()
    {
        var weekly = CreateWeeklyItems(12);
        var discovery = new RecordingDiscoveryService([]);
        var service = CreateService(new FakeSnapshotService(weekly), discovery);

        var result = await service.ComposeAsync(SearchContentType.All, 3, 4, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(HomeWeeklyTrendingReadSource.WeeklySnapshot, result.ReadSource);
        Assert.Equal(
            ["Weekly 1", "Weekly 2", "Weekly 3"],
            result.HeroItems.Select(item => item.Title).ToList());
        Assert.Equal(
            ["Weekly 4", "Weekly 5", "Weekly 6", "Weekly 7"],
            result.TrendingItems.Select(item => item.Title).ToList());
        Assert.Equal(0, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task ComposeAsyncExcludesHeroItemsFromTrendingByContentIdentity()
    {
        var sharedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        List<SearchItem> weekly =
        [
            CreateSearchItem("movie", sharedId, "Weekly Movie"),
            CreateSearchItem("tv", sharedId, "Weekly Tv Same Id"),
            CreateSearchItem("movie", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Weekly Movie Two"),
        ];

        var service = CreateService(new FakeSnapshotService(weekly), new RecordingDiscoveryService([]));

        var result = await service.ComposeAsync(SearchContentType.All, 1, 5, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.HeroItems);
        Assert.Equal(2, result.TrendingItems.Count);
        Assert.DoesNotContain(result.TrendingItems, item => item.Type == "movie" && item.Id == sharedId);
        Assert.Contains(result.TrendingItems, item => item.Type == "tv" && item.Id == sharedId);
    }

    [Fact]
    public async Task ComposeAsyncFiltersMovieTypeBeforeSplitting()
    {
        List<SearchItem> weekly =
        [
            CreateSearchItem("tv", Guid.Parse("11111111-1111-1111-1111-111111111101"), "Tv One"),
            CreateSearchItem("movie", Guid.Parse("22222222-2222-2222-2222-222222222201"), "Movie One"),
            CreateSearchItem("movie", Guid.Parse("33333333-3333-3333-3333-333333333301"), "Movie Two"),
        ];

        var service = CreateService(new FakeSnapshotService(weekly), new RecordingDiscoveryService([]));

        var result = await service.ComposeAsync(SearchContentType.Movie, 1, 2, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Movie One"], result.HeroItems.Select(item => item.Title).ToList());
        Assert.Equal(["Movie Two"], result.TrendingItems.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task ComposeAsyncFiltersTvTypeBeforeSplitting()
    {
        List<SearchItem> weekly =
        [
            CreateSearchItem("movie", Guid.Parse("11111111-1111-1111-1111-111111111101"), "Movie One"),
            CreateSearchItem("tv", Guid.Parse("22222222-2222-2222-2222-222222222201"), "Tv One"),
            CreateSearchItem("tv", Guid.Parse("33333333-3333-3333-3333-333333333301"), "Tv Two"),
        ];

        var service = CreateService(new FakeSnapshotService(weekly), new RecordingDiscoveryService([]));

        var result = await service.ComposeAsync(SearchContentType.Tv, 1, 2, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Tv One"], result.HeroItems.Select(item => item.Title).ToList());
        Assert.Equal(["Tv Two"], result.TrendingItems.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task ComposeAsyncReturnsShortTrendingRailWhenTypedWeeklyPoolIsThin()
    {
        List<SearchItem> weekly =
        [
            CreateSearchItem("movie", Guid.Parse("11111111-1111-1111-1111-111111111101"), "Movie One"),
            CreateSearchItem("tv", Guid.Parse("22222222-2222-2222-2222-222222222201"), "Tv One"),
        ];

        var discovery = new RecordingDiscoveryService(
        [
            CreateSearchItem("movie", Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Catalog Popular"),
        ]);
        var service = CreateService(new FakeSnapshotService(weekly), discovery);

        var result = await service.ComposeAsync(SearchContentType.Movie, 1, 5, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(HomeWeeklyTrendingReadSource.WeeklySnapshot, result.ReadSource);
        Assert.Single(result.HeroItems);
        Assert.Empty(result.TrendingItems);
        Assert.Equal(0, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task ComposeAsyncUsesSingleCatalogFallbackRequestWhenSnapshotMissing()
    {
        var fallbackItems = CreateWeeklyItems(8);
        var discovery = new RecordingDiscoveryService(fallbackItems);
        var service = CreateService(new FakeSnapshotService(null), discovery);

        var result = await service.ComposeAsync(SearchContentType.All, 3, 3, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(HomeWeeklyTrendingReadSource.CatalogFallback, result.ReadSource);
        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(6, discovery.LastTrendingPageSize);
        Assert.Equal(
            ["Weekly 1", "Weekly 2", "Weekly 3"],
            result.HeroItems.Select(item => item.Title).ToList());
        Assert.Equal(
            ["Weekly 4", "Weekly 5", "Weekly 6"],
            result.TrendingItems.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SplitPreservesWeeklyOrder()
    {
        var weekly = CreateWeeklyItems(6);
        var (hero, trending) = HomeWeeklyTrendingComposition.Split(weekly, 2, 3);

        Assert.Equal(["Weekly 1", "Weekly 2"], hero.Select(item => item.Title).ToList());
        Assert.Equal(["Weekly 3", "Weekly 4", "Weekly 5"], trending.Select(item => item.Title).ToList());
    }

    private static HomeWeeklyTrendingCompositionService CreateService(
        FakeSnapshotService snapshotService,
        RecordingDiscoveryService discoveryService) =>
        new(
            snapshotService,
            discoveryService,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());

    private static List<SearchItem> CreateWeeklyItems(int count) =>
        Enumerable.Range(1, count)
            .Select(index =>
            {
                var type = index % 2 == 0 ? "tv" : "movie";
                return CreateSearchItem(
                    type,
                    Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{index:D12}"),
                    $"Weekly {index}");
            })
            .ToList();

    private static SearchItem CreateSearchItem(string type, Guid id, string title) =>
        new(id, type, title, null, null, "/poster.jpg", null, null, 8m, 100, null);

    private sealed class FakeSnapshotService(IReadOnlyList<SearchItem>? items) : IHotThisWeekTrendingSnapshotService
    {
        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (items is null || items.Count == 0)
            {
                return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(null);
            }

            return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(new HotThisWeekTrendingSnapshotEntry
            {
                RefreshedAt = DateTimeOffset.UtcNow,
                Items = items,
            });
        }

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDiscoveryService(IReadOnlyList<SearchItem> trendingItems) : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public int LastTrendingPageSize { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            LastTrendingPageSize = criteria.PageSize;

            var items = trendingItems
                .Where(item => criteria.Type switch
                {
                    SearchContentType.Movie => item.Type == "movie",
                    SearchContentType.Tv => item.Type == "tv",
                    _ => item.Type is "movie" or "tv",
                })
                .Take(criteria.PageSize)
                .ToList();

            return Task.FromResult(new PaginatedResult<SearchItem>(
                items,
                criteria.Page,
                criteria.PageSize,
                items.Count,
                items.Count == 0 ? 0 : 1));
        }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
