using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverBrowseMergerTests
{
    [Fact]
    public void MergeOrdersAllTypeResultsDeterministicallyWithoutMovieFirstBias()
    {
        var movieId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tvId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var movieItem = CreateItem(movieId, "movie", voteAverage: 8.0m, voteCount: 1000);
        var tvItem = CreateItem(tvId, "tv", voteAverage: 8.0m, voteCount: 1000);

        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.TopRated,
            SearchContentType.All,
            [],
            null,
            null,
            null,
            DiscoverBrowseSort.RatingDesc,
            1,
            20);

        var result = DiscoverBrowseMerger.Merge(
            criteria,
            [movieItem],
            [tvItem],
            movieTotalCount: 1,
            tvTotalCount: 1);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("movie", result.Items[0].Type);
        Assert.Equal(movieId, result.Items[0].Id);
        Assert.Equal("tv", result.Items[1].Type);
        Assert.Equal(tvId, result.Items[1].Id);
    }

    [Fact]
    public void MergeCapsAllTypePageToRequestedPageSize()
    {
        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.Trending,
            SearchContentType.All,
            [],
            null,
            null,
            null,
            null,
            1,
            1);

        var movieItems = Enumerable.Range(1, 3)
            .Select(index => CreateItem(Guid.NewGuid(), "movie", voteCount: 100 - index))
            .ToList();
        var tvItems = Enumerable.Range(1, 3)
            .Select(index => CreateItem(Guid.NewGuid(), "tv", voteCount: 50 - index))
            .ToList();

        var result = DiscoverBrowseMerger.Merge(
            criteria,
            movieItems,
            tvItems,
            movieTotalCount: 3,
            tvTotalCount: 3);

        Assert.Single(result.Items);
        Assert.Equal(6, result.TotalCount);
    }

    private static SearchItem CreateItem(
        Guid id,
        string type,
        decimal voteAverage = 7m,
        int voteCount = 100) =>
        new(
            id,
            type,
            $"Title {id:N}",
            null,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(2024, 1, 1),
            voteAverage,
            voteCount,
            2024);
}
