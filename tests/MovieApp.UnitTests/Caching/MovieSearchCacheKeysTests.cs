using MovieApp.Application.Caching;
using MovieApp.Application.Models.Movies;

namespace MovieApp.UnitTests.Caching;

public sealed class MovieSearchCacheKeysTests
{
    [Theory]
    [InlineData("Interstellar", 1, 20, "movie-search:interstellar:page:1:size:20")]
    [InlineData(" INTERSTELLAR ", 1, 20, "movie-search:interstellar:page:1:size:20")]
    [InlineData("InTeRsTeLLaR", 2, 10, "movie-search:interstellar:page:2:size:10")]
    public void CreateProducesNormalizedCacheKeyWithPagination(
        string query,
        int page,
        int pageSize,
        string expectedKey)
    {
        Assert.Equal(expectedKey, MovieSearchCacheKeys.Create(query, page, pageSize));
    }

    [Fact]
    public void CreateProducesDistinctKeysForDifferentPaginationParameters()
    {
        var pageOneKey = MovieSearchCacheKeys.Create(
            "interstellar",
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize);

        var pageTwoKey = MovieSearchCacheKeys.Create("interstellar", 2, MovieSearchPagination.DefaultPageSize);
        var differentSizeKey = MovieSearchCacheKeys.Create("interstellar", 1, 50);

        Assert.NotEqual(pageOneKey, pageTwoKey);
        Assert.NotEqual(pageOneKey, differentSizeKey);
    }
}
