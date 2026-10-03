using MovieApp.Application.Models.Library;
using MovieApp.Application.Services.Library;

namespace MovieApp.UnitTests.Library;

public sealed class LibrarySearchAggregatorTests
{
    [Fact]
    public void MergeByTitlePriority_DedupesAcrossCategoriesAndPrefersWatching()
    {
        var watchingId = Guid.NewGuid();
        var watchedOnlyId = Guid.NewGuid();

        var watching = new LibraryItemResult(
            watchingId,
            "movie",
            "Thor",
            null,
            null,
            null,
            2011,
            7.5m,
            null,
            null,
            null,
            null,
            null,
            "watching");

        var watchedDuplicate = watching with { CollectionStatus = "watched" };
        var watchedOnly = new LibraryItemResult(
            watchedOnlyId,
            "movie",
            "Thunderbolts",
            null,
            null,
            null,
            2025,
            7m,
            null,
            null,
            null,
            null,
            null,
            "watched");

        var watchlistOnlyId = Guid.NewGuid();
        var watchlistOnly = new LibraryItemResult(
            watchlistOnlyId,
            "movie",
            "Titanic",
            null,
            null,
            null,
            1997,
            7.9m,
            null,
            null,
            null,
            null,
            null,
            "watchlist");

        var merged = LibrarySearchAggregator.MergeByTitlePriority(
            new Dictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>>
            {
                [LibraryCategory.Watching] = [watching],
                [LibraryCategory.Watched] = [watchedDuplicate, watchedOnly],
                [LibraryCategory.Liked] = [],
                [LibraryCategory.Watchlist] = [watching with { CollectionStatus = "watchlist" }, watchlistOnly],
            });

        Assert.Equal(3, merged.Count);
        Assert.Equal("Thor", merged[0].Title);
        Assert.Equal("watching", merged[0].CollectionStatus);
        Assert.Equal("Thunderbolts", merged[1].Title);
        Assert.Equal("Titanic", merged[2].Title);
        Assert.Equal("watchlist", merged[2].CollectionStatus);
    }
}
