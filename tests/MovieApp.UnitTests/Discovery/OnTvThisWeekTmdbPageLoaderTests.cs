using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekTmdbPageLoaderTests
{
    [Fact]
    public async Task LoadOrderedSummariesAsyncDoesNotFetchPageTwoWhenPageOneFillsSectionSize()
    {
        var catalog = new RecordingCatalog(new Dictionary<int, IReadOnlyList<TvShowProviderSummary>>
        {
            [1] = Enumerable.Range(1, 5)
                .Select(index => Scripted(index))
                .ToList(),
        });

        var result = await OnTvThisWeekTmdbPageLoader.LoadOrderedSummariesAsync(
            catalog,
            requestPage: 1,
            pageSize: 3,
            OnTvThisWeekPresentationIntent.HomeRail,
            CancellationToken.None);

        Assert.Equal([1], catalog.RequestedPages);
        Assert.Equal(1, result.TmdbPagesFetched);
        Assert.True(result.OrderedSummaries.Count >= 3);
    }

    [Fact]
    public async Task LoadOrderedSummariesAsyncFetchesPageTwoWhenPageOneCannotFillAfterTalkExclusion()
    {
        var catalog = new RecordingCatalog(new Dictionary<int, IReadOnlyList<TvShowProviderSummary>>
        {
            [1] =
            [
                Talk(101),
                Talk(102),
                Talk(103),
                Scripted(1),
            ],
            [2] =
            [
                Scripted(2),
                Scripted(3),
            ],
        });

        var result = await OnTvThisWeekTmdbPageLoader.LoadOrderedSummariesAsync(
            catalog,
            requestPage: 1,
            pageSize: 3,
            OnTvThisWeekPresentationIntent.HomeRail,
            CancellationToken.None);

        Assert.Equal([1, 2], catalog.RequestedPages);
        Assert.Equal(2, result.TmdbPagesFetched);
        Assert.Equal([1, 2, 3], result.OrderedSummaries.Select(summary => summary.TmdbId).ToArray());
        Assert.DoesNotContain(result.OrderedSummaries, summary => summary.GenreTmdbIds!.Contains(OnTvThisWeekContentSelector.TalkTmdbGenreId));
    }

    [Fact]
    public async Task LoadOrderedSummariesAsyncNeverFetchesBeyondPageTwoForRequestPageOne()
    {
        var catalog = new RecordingCatalog(new Dictionary<int, IReadOnlyList<TvShowProviderSummary>>
        {
            [1] = [Talk(201), Talk(202)],
            [2] = [Talk(203)],
        });

        var result = await OnTvThisWeekTmdbPageLoader.LoadOrderedSummariesAsync(
            catalog,
            requestPage: 1,
            pageSize: 5,
            OnTvThisWeekPresentationIntent.HomeRail,
            CancellationToken.None);

        Assert.Equal([1, 2], catalog.RequestedPages);
        Assert.Equal(2, result.TmdbPagesFetched);
        Assert.Empty(result.OrderedSummaries);
    }

    [Fact]
    public async Task LoadOrderedSummariesAsyncDedupesSummariesAcrossPages()
    {
        var duplicate = Scripted(42);
        var catalog = new RecordingCatalog(new Dictionary<int, IReadOnlyList<TvShowProviderSummary>>
        {
            [1] = [Talk(1), duplicate],
            [2] = [duplicate, Scripted(43)],
        });

        var result = await OnTvThisWeekTmdbPageLoader.LoadOrderedSummariesAsync(
            catalog,
            requestPage: 1,
            pageSize: 3,
            OnTvThisWeekPresentationIntent.HomeRail,
            CancellationToken.None);

        Assert.Equal(2, result.OrderedSummaries.Count);
        Assert.Equal([42, 43], result.OrderedSummaries.Select(summary => summary.TmdbId).ToArray());
    }

    [Fact]
    public async Task LoadOrderedSummariesAsyncHomeRailExcludesNewsAndRealityAfterMerge()
    {
        var catalog = new RecordingCatalog(new Dictionary<int, IReadOnlyList<TvShowProviderSummary>>
        {
            [1] =
            [
                News(1),
                Scripted(10),
            ],
            [2] =
            [
                Reality(2),
                Scripted(11),
            ],
        });

        var result = await OnTvThisWeekTmdbPageLoader.LoadOrderedSummariesAsync(
            catalog,
            requestPage: 1,
            pageSize: 4,
            OnTvThisWeekPresentationIntent.HomeRail,
            CancellationToken.None);

        Assert.Equal([10, 11], result.OrderedSummaries.Select(summary => summary.TmdbId).ToArray());
    }

    [Fact]
    public async Task LoadOrderedSummariesAsyncDiscoverBrowseExcludesNewsAndRealityWithoutBackfill()
    {
        var catalog = new RecordingCatalog(new Dictionary<int, IReadOnlyList<TvShowProviderSummary>>
        {
            [1] =
            [
                News(1),
                Scripted(10),
                Reality(2),
            ],
        });

        var result = await OnTvThisWeekTmdbPageLoader.LoadOrderedSummariesAsync(
            catalog,
            requestPage: 1,
            pageSize: 1,
            OnTvThisWeekPresentationIntent.DiscoverBrowse,
            CancellationToken.None);

        Assert.Equal([10], result.OrderedSummaries.Select(summary => summary.TmdbId).ToArray());
        Assert.Equal([1], catalog.RequestedPages);
    }

    [Fact]
    public async Task LoadOrderedSummariesAsyncRequestPageTwoFetchesNextTmdbPageWhenDiscoverPageIsShort()
    {
        var catalog = new RecordingCatalog(new Dictionary<int, IReadOnlyList<TvShowProviderSummary>>
        {
            [2] = [Talk(1), Talk(2), Scripted(50)],
            [3] = [Scripted(51), Scripted(52)],
        });

        var result = await OnTvThisWeekTmdbPageLoader.LoadOrderedSummariesAsync(
            catalog,
            requestPage: 2,
            pageSize: 3,
            OnTvThisWeekPresentationIntent.DiscoverBrowse,
            CancellationToken.None);

        Assert.Equal([2, 3], catalog.RequestedPages);
        Assert.Equal(2, result.TmdbPagesFetched);
        Assert.Equal([50, 51, 52], result.OrderedSummaries.Select(summary => summary.TmdbId).ToArray());
    }

    private static TvShowProviderSummary Scripted(int tmdbId) =>
        Summary(tmdbId, [18], 100);

    private static TvShowProviderSummary Talk(int tmdbId) =>
        Summary(tmdbId, [OnTvThisWeekContentSelector.TalkTmdbGenreId], 100);

    private static TvShowProviderSummary News(int tmdbId) =>
        Summary(tmdbId, [OnTvThisWeekContentSelector.NewsTmdbGenreId], 100);

    private static TvShowProviderSummary Reality(int tmdbId) =>
        Summary(tmdbId, [OnTvThisWeekContentSelector.RealityTmdbGenreId], 100);

    private static TvShowProviderSummary Summary(int tmdbId, int[] genreIds, int voteCount) =>
        new(
            $"fake-{tmdbId}",
            tmdbId,
            null,
            null,
            $"Title {tmdbId}",
            $"Title {tmdbId}",
            "Overview",
            new DateOnly(2024, 1, 1),
            "/poster.jpg",
            null,
            "en",
            8m,
            voteCount,
            GenreTmdbIds: genreIds);

    private sealed class RecordingCatalog(IReadOnlyDictionary<int, IReadOnlyList<TvShowProviderSummary>> pages)
        : IOnTvThisWeekCatalog
    {
        public List<int> RequestedPages { get; } = [];

        public Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
            int page,
            CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(page);
            var items = pages.TryGetValue(page, out var pageItems) ? pageItems : [];
            return Task.FromResult(new TvShowProviderSearchResult(
                items,
                page,
                20,
                100,
                5));
        }
    }
}
