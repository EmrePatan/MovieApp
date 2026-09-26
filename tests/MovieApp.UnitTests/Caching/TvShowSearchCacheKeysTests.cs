using MovieApp.Application.Caching;
using MovieApp.Application.Models.Common;

namespace MovieApp.UnitTests.Caching;

public sealed class TvShowSearchCacheKeysTests
{
    [Fact]
    public void CreateProducesNormalizedCacheKeyWithPagination()
    {
        var key = TvShowSearchCacheKeys.Create(" Breaking Bad ", 1, 20);

        Assert.Equal("tvshow-search:Breaking Bad:page:1:size:20", key);
    }

    [Fact]
    public void CreateProducesDistinctKeysForDifferentPaginationParameters()
    {
        var pageOne = TvShowSearchCacheKeys.Create("breaking", 1, SearchPaginationDefaults.DefaultPageSize);
        var pageTwo = TvShowSearchCacheKeys.Create("breaking", 2, SearchPaginationDefaults.DefaultPageSize);

        Assert.NotEqual(pageOne, pageTwo);
        Assert.StartsWith("tvshow-search:", pageOne, StringComparison.Ordinal);
    }
}
