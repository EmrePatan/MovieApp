using MovieApp.Application.Models.Search;
using MovieApp.Application.Search;

namespace MovieApp.UnitTests.Search;

public sealed class SearchKeysetCursorTests
{
    [Fact]
    public void EncodeDecodeRoundTripPreservesFingerprint()
    {
        var criteria = new SearchCriteria(
            "Batman",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var item = new SearchItem(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "movie",
            "Batman Begins",
            null,
            null,
            null,
            null,
            null,
            8.5m,
            500,
            null,
            123,
            null);

        var cursor = SearchKeysetCursor.CreateFromItem(
            item,
            criteria,
            "batman",
            1,
            42,
            SearchKeysetCursor.ComputeRelevanceTier(item, "batman"));
        var encoded = SearchKeysetCursor.Encode(cursor);

        Assert.True(SearchKeysetCursor.TryDecode(encoded, criteria, "batman", out var decoded, out _));
        Assert.Equal(cursor.Id, decoded!.Id);
        Assert.Equal(cursor.Fingerprint, decoded.Fingerprint);
        Assert.Equal(42, decoded.SnapshotTotalCount);
    }

    [Fact]
    public void TryDecodeRejectsMismatchedSearchCriteria()
    {
        var criteria = new SearchCriteria(
            "Batman",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var item = new SearchItem(
            Guid.NewGuid(),
            "movie",
            "Batman",
            null,
            null,
            null,
            null,
            null,
            1,
            1,
            null,
            null,
            null);

        var encoded = SearchKeysetCursor.Encode(SearchKeysetCursor.CreateFromItem(
            item,
            criteria,
            "batman",
            1,
            10,
            SearchKeysetCursor.ComputeRelevanceTier(item, "batman")));
        var otherCriteria = criteria with { Sort = SearchSortOption.TitleAsc };

        Assert.False(SearchKeysetCursor.TryDecode(encoded, otherCriteria, "batman", out _, out var error));
        Assert.Contains("match", error!, StringComparison.OrdinalIgnoreCase);
    }
}
