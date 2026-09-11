using MovieApp.Application.Models.Movies;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.UnitTests.Providers;

public sealed class FakeMovieDataProviderTests
{
    [Fact]
    public async Task SearchMoviesAsyncReturnsInterstellarForNormalizedQuery()
    {
        var provider = new FakeMovieDataProvider(new MovieDataProviderCallTracker());

        var result = await provider.SearchMoviesAsync(
            " INTERSTELLAR ",
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize);

        Assert.Single(result.Results);
        Assert.Equal("Interstellar", result.Results[0].Title);
        Assert.Equal(FakeMovieDataProvider.InterstellarTmdbId, result.Results[0].TmdbId);
        Assert.Equal(FakeMovieDataProvider.InterstellarExternalId, result.Results[0].ExternalId);
        Assert.Equal(1, result.Page);
        Assert.Equal(MovieSearchPagination.DefaultPageSize, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task SearchMoviesAsyncReturnsEmptyPageBeyondAvailableResults()
    {
        var provider = new FakeMovieDataProvider(new MovieDataProviderCallTracker());

        var result = await provider.SearchMoviesAsync("interstellar", 2, 20);

        Assert.Empty(result.Results);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task SearchMoviesAsyncReturnsPagedCatalogWithMetadata()
    {
        var provider = new FakeMovieDataProvider(new MovieDataProviderCallTracker());

        var result = await provider.SearchMoviesAsync(
            FakeMovieDataProvider.PagedCatalogQueryToken,
            1,
            10);

        Assert.Equal(10, result.Results.Count);
        Assert.Equal("Fake Movie 1", result.Results[0].Title);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(FakeMovieDataProvider.PagedCatalogMovieCount, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task SearchMoviesAsyncReturnsSecondPageOfPagedCatalog()
    {
        var provider = new FakeMovieDataProvider(new MovieDataProviderCallTracker());

        var result = await provider.SearchMoviesAsync(
            FakeMovieDataProvider.PagedCatalogQueryToken,
            2,
            10);

        Assert.Equal(10, result.Results.Count);
        Assert.Equal("Fake Movie 11", result.Results[0].Title);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task GetMovieAsyncReturnsDetailsWithGenres()
    {
        var provider = new FakeMovieDataProvider(new MovieDataProviderCallTracker());

        var details = await provider.GetMovieAsync(FakeMovieDataProvider.InterstellarExternalId);

        Assert.NotNull(details);
        Assert.Equal("tt9000001", details.ImdbId);
        Assert.Equal(3, details.Genres.Count);
        Assert.Contains("Science Fiction", details.Genres);
    }
}
