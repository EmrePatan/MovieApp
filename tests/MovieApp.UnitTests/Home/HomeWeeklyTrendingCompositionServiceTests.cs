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

    [Fact]
    public void SplitIncludesLowVoteAverageInHeroInWeeklyOrder()
    {
        List<SearchItem> weekly =
        [
            CreateEligibleItem("movie", 1, "Resident Evil", voteAverage: 6.1m, voteCount: 12),
            CreateEligibleItem("movie", 2, "High Rated Skip", voteAverage: 8.2m, backdropUrl: null),
            CreateEligibleItem("movie", 3, "Futurama", voteAverage: 8.4m, voteCount: 5000),
            CreateEligibleItem("movie", 4, "Another Hot", voteAverage: 5.9m, voteCount: 3),
        ];

        var (hero, trending) = HomeWeeklyTrendingComposition.Split(weekly, 3, 4);

        Assert.Equal(
            ["Resident Evil", "Futurama", "Another Hot"],
            hero.Select(item => item.Title).ToList());
        Assert.Equal(["High Rated Skip"], trending.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SplitExcludesMissingBackdropFromHeroButKeepsInTrending()
    {
        List<SearchItem> weekly =
        [
            CreateEligibleItem("movie", 1, "With Backdrop"),
            CreateEligibleItem("movie", 2, "No Backdrop", backdropUrl: null),
            CreateEligibleItem("movie", 3, "Also With Backdrop"),
        ];

        var (hero, trending) = HomeWeeklyTrendingComposition.Split(weekly, 2, 3);

        Assert.Equal(["With Backdrop", "Also With Backdrop"], hero.Select(item => item.Title).ToList());
        Assert.Equal(["No Backdrop"], trending.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SplitIncludesLowVoteCountInHeroInWeeklyOrder()
    {
        List<SearchItem> weekly =
        [
            CreateEligibleItem("movie", 1, "Hot Now", voteCount: 5),
            CreateEligibleItem("movie", 2, "Also Hot", voteCount: 8),
        ];

        var (hero, trending) = HomeWeeklyTrendingComposition.Split(weekly, 2, 3);

        Assert.Equal(["Hot Now", "Also Hot"], hero.Select(item => item.Title).ToList());
        Assert.Empty(trending);
    }

    [Fact]
    public void SplitReturnsShorterHeroWhenInsufficientEligibleCandidates()
    {
        List<SearchItem> weekly =
        [
            CreateEligibleItem("movie", 1, "Only Hero"),
            CreateEligibleItem("movie", 2, "No Backdrop", backdropUrl: null),
        ];

        var (hero, trending) = HomeWeeklyTrendingComposition.Split(weekly, 3, 5);

        Assert.Single(hero);
        Assert.Equal(["No Backdrop"], trending.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SplitTrendingPreservesOriginalWeeklyOrderAfterHeroRemoval()
    {
        List<SearchItem> weekly =
        [
            CreateEligibleItem("movie", 1, "Hero A"),
            CreateEligibleItem("movie", 2, "Not Hero", backdropUrl: null),
            CreateEligibleItem("movie", 3, "Hero B"),
            CreateEligibleItem("movie", 4, "Trend C", voteAverage: 5m),
            CreateEligibleItem("movie", 5, "Trend D"),
        ];

        var (hero, trending) = HomeWeeklyTrendingComposition.Split(weekly, 2, 4);

        Assert.Equal(["Hero A", "Hero B"], hero.Select(item => item.Title).ToList());
        Assert.Equal(
            ["Not Hero", "Trend C", "Trend D"],
            trending.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SplitDoesNotPromoteHigherRatedItemAheadOfLowerRatedWeeklyItem()
    {
        List<SearchItem> weekly =
        [
            CreateEligibleItem("movie", 1, "Lower Rated First", voteAverage: 6m),
            CreateEligibleItem("movie", 2, "Higher Rated Second", voteAverage: 9.5m),
        ];

        var (hero, _) = HomeWeeklyTrendingComposition.Split(weekly, 2, 2);

        Assert.Equal(
            ["Lower Rated First", "Higher Rated Second"],
            hero.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SplitDoesNotDuplicateHeroItemsInTrending()
    {
        var weekly = CreateWeeklyItems(5);
        var (hero, trending) = HomeWeeklyTrendingComposition.Split(weekly, 2, 5);

        Assert.Equal(2, hero.Count);
        Assert.DoesNotContain(
            trending,
            item => hero.Any(heroItem => heroItem.Type == item.Type && heroItem.Id == item.Id));
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
        CreateEligibleItem(type, 0, title, id);

    private static SearchItem CreateEligibleItem(
        string type,
        int seed,
        string title,
        Guid? id = null,
        string? backdropUrl = "/backdrop.jpg",
        decimal voteAverage = 8m,
        int voteCount = 100) =>
        new(
            id ?? Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{seed:D12}"),
            type,
            title,
            null,
            null,
            "/poster.jpg",
            backdropUrl,
            null,
            voteAverage,
            voteCount,
            null);

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
