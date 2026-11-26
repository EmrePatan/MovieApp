using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Application.Services.StreamingLeaving;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.StreamingLeaving;

public sealed class StreamingLeavingListBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc);

    private static readonly StreamingLeavingCriteria All = new(
        "TR", SearchContentType.All, 1, 20, [], false, null, null, null, null, null, null, null, null, null,
        [], StreamingLeavingSort.LeavingAsc);

    [Fact]
    public void FewerThanFiveTitlesGiveAnEmptyList()
    {
        var rows = Enumerable.Range(1, 4).Select(day => Row($"T{day}", day)).ToList();
        // A second platform of the same title does not count as another title.
        rows.Add(rows[0] with { ServiceId = "prime" });

        var result = StreamingLeavingListBuilder.Build(rows, All, 5);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public void ATitleLeavingTwoServicesIsOneCardWithTheNearestDate()
    {
        var rows = Five();
        rows.Add(rows[4] with { ServiceId = "prime", ServiceName = "Prime", LeavesAtUtc = Now.AddDays(1) });

        var result = StreamingLeavingListBuilder.Build(rows, All, 5);

        Assert.Equal(5, result.TotalCount);
        var card = result.Items.Single(item => item.Item.Title == "T5");
        Assert.Equal(Now.AddDays(1), card.LeavesAtUtc);
        Assert.Equal(["prime", "netflix"], card.Platforms.Select(platform => platform.ServiceId));
    }

    [Fact]
    public void MyPlatformsKeepsMatchingTitlesAndUsesTheirDate()
    {
        var rows = Five();
        rows.Add(rows[4] with { ServiceId = "prime", ServiceName = "Prime", LeavesAtUtc = Now.AddDays(1) });

        var netflix = StreamingLeavingListBuilder.Build(rows, All with { WatchProviderIds = [8] }, 5);
        var prime = StreamingLeavingListBuilder.Build(rows, All with { WatchProviderIds = [119] }, 5);

        Assert.Equal(Now.AddDays(5), netflix.Items.Single(item => item.Item.Title == "T5").LeavesAtUtc);
        Assert.Equal("T5", Assert.Single(prime.Items).Item.Title);
    }

    [Fact]
    public void FiltersMayLeaveFewerThanFiveTitles()
    {
        var rows = Five();
        var genre = Guid.NewGuid();
        rows[1] = rows[1] with { GenreIds = [genre], VoteAverage = 8.5m, VoteCount = 500 };
        rows[2] = rows[2] with { MediaType = CatalogContentType.Tv, RuntimeMinutes = null };

        Assert.Equal("T2", Single(All with { GenreIds = [genre] }));
        Assert.Equal("T2", Single(All with { MinRating = 8 }));
        Assert.Equal("T2", Single(All with { MinVoteCount = 100 }));
        Assert.Equal("T3", Single(All with { MediaType = SearchContentType.Tv }));
        // A TV show has no stored runtime.
        Assert.DoesNotContain(
            StreamingLeavingListBuilder.Build(rows, All with { MinRuntimeMinutes = 1 }, 5).Items,
            item => item.Item.Title == "T3");

        string Single(StreamingLeavingCriteria criteria) =>
            Assert.Single(StreamingLeavingListBuilder.Build(rows, criteria, 5).Items).Item.Title;
    }

    [Fact]
    public void DefaultSortIsNearestFirstAndPagesDoNotOverlap()
    {
        var rows = Five();
        rows.Reverse();

        var first = StreamingLeavingListBuilder.Build(rows, All with { PageSize = 2 }, 5);
        var second = StreamingLeavingListBuilder.Build(rows, All with { Page = 2, PageSize = 2 }, 5);
        var byTitle = StreamingLeavingListBuilder.Build(
            rows, All with { Sort = StreamingLeavingSort.TitleDesc }, 5);

        Assert.Equal(["T1", "T2"], first.Items.Select(item => item.Item.Title));
        Assert.Equal(["T3", "T4"], second.Items.Select(item => item.Item.Title));
        Assert.Equal(3, first.TotalPages);
        Assert.Equal("T5", byTitle.Items[0].Item.Title);
    }

    private static List<StreamingLeavingRow> Five() =>
        Enumerable.Range(1, 5).Select(day => Row($"T{day}", day)).ToList();

    private static StreamingLeavingRow Row(string title, int days) =>
        new(
            "netflix", "Netflix", null, Now.AddDays(days), CatalogContentType.Movie, Guid.NewGuid(), days,
            title, null, null, null, null, new DateOnly(2020, 1, 1), 100, "en", "US", 6m, 10, []);
}
