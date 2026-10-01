using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverKeywordFilterGuardTests
{
    [Fact]
    public void IsUnresolvedKeywordFilter_WhenKeywordIdsPresentAndNoTmdbIds_ReturnsTrue()
    {
        var unresolved = DiscoverKeywordFilterGuard.IsUnresolvedKeywordFilter(
            [Guid.NewGuid()],
            []);

        Assert.True(unresolved);
    }

    [Fact]
    public void IsUnresolvedKeywordFilter_WhenTmdbIdsResolved_ReturnsFalse()
    {
        var resolved = DiscoverKeywordFilterGuard.IsUnresolvedKeywordFilter(
            [Guid.NewGuid()],
            [9715]);

        Assert.False(resolved);
    }

    [Fact]
    public void CreateEmptyBrowseResult_ReturnsZeroTotal()
    {
        var result = DiscoverKeywordFilterGuard.CreateEmptyBrowseResult(2, 20);

        Assert.Empty(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(0, result.TotalCount);
    }
}
