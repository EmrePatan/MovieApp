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

        var merged = LibrarySearchAggregator.MergeByTitlePriority(
            new Dictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>>
            {
                [LibraryCategory.Watching] = [watching],
                [LibraryCategory.Watched] = [watchedDuplicate, watchedOnly],
                [LibraryCategory.Liked] = [],
            });

        Assert.Equal(2, merged.Count);
        Assert.Equal("Thor", merged[0].Title);
        Assert.Equal("watching", merged[0].CollectionStatus);
        Assert.Equal("Thunderbolts", merged[1].Title);
    }
}
