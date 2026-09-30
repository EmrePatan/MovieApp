using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverCatalogSortPolicyTests
{
    [Fact]
    public void ResolveBrowseMinVoteCount_TopRatedModeUsesCuratedFloor()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.TopRated,
            DiscoverBrowseSort.RatingDesc,
            null);

        Assert.Equal(DiscoverCatalogSortPolicy.CuratedTopRatedListMinimumVoteCount, effective);
    }

    [Fact]
    public void ResolveBrowseMinVoteCount_TopRatedModePreservesStricterUserVoteCount()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.TopRated,
            DiscoverBrowseSort.RatingDesc,
            5000);

        Assert.Equal(5000, effective);
    }

    [Fact]
    public void ResolveBrowseMinVoteCount_TrendingDoesNotApplyCuratedFloor()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.Trending,
            DiscoverBrowseSort.PopularityDesc,
            null);

        Assert.Null(effective);
    }

    [Fact]
    public void ResolveBrowseMinVoteCount_TrendingRatingSortUsesRatingRankedFloorOnly()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.Trending,
            DiscoverBrowseSort.RatingDesc,
            null);

        Assert.Equal(DiscoverCatalogSortPolicy.RatingRankedSortMinimumVoteCount, effective);
    }
}
