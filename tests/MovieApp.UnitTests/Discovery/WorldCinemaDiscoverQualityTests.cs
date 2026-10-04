using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class WorldCinemaDiscoverQualityTests
{
    [Theory]
    [InlineData(SearchContentType.Movie, AdvancedDiscoverSort.PopularityDesc, 50)]
    [InlineData(SearchContentType.Tv, AdvancedDiscoverSort.PopularityDesc, 50)]
    [InlineData(SearchContentType.Movie, AdvancedDiscoverSort.Oldest, 50)]
    [InlineData(SearchContentType.Movie, AdvancedDiscoverSort.RatingDesc, 100)]
    [InlineData(SearchContentType.Tv, AdvancedDiscoverSort.RatingDesc, 75)]
    [InlineData(SearchContentType.Movie, AdvancedDiscoverSort.Newest, 1)]
    [InlineData(SearchContentType.Tv, AdvancedDiscoverSort.Newest, 1)]
    public void ResolveMinimumVoteCountReturnsPolicyFloors(
        SearchContentType mediaType,
        AdvancedDiscoverSort sort,
        int expected)
    {
        Assert.Equal(expected, WorldCinemaDiscoverQuality.ResolveMinimumVoteCount(mediaType, sort));
    }
}
