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

    [Fact]
    public void ResolveBrowseMinVoteCount_NewReleasesUsesCallerFloor()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.NewReleases,
            DiscoverBrowseSort.ReleaseDesc,
            null,
            newReleasesVoteFloor: 75);

        Assert.Equal(75, effective);
    }

    [Fact]
    public void ResolveBrowseMinVoteCount_NewReleasesKeepsStricterUserVoteCount()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.NewReleases,
            DiscoverBrowseSort.ReleaseDesc,
            500,
            newReleasesVoteFloor: 75);

        Assert.Equal(500, effective);
    }

    [Fact]
    public void ResolveBrowseMinVoteCount_NewReleasesRaisesWeakerUserVoteCount()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.NewReleases,
            DiscoverBrowseSort.ReleaseDesc,
            10,
            newReleasesVoteFloor: 75);

        Assert.Equal(75, effective);
    }

    [Fact]
    public void ResolveBrowseMinVoteCount_NewReleasesRatingSortUsesTheHigherFloor()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.NewReleases,
            DiscoverBrowseSort.RatingDesc,
            null,
            newReleasesVoteFloor: 40);

        Assert.Equal(DiscoverCatalogSortPolicy.RatingRankedSortMinimumVoteCount, effective);
    }

    [Fact]
    public void ResolveBrowseMinVoteCount_TrendingIgnoresNewReleasesFloor()
    {
        var effective = DiscoverCatalogSortPolicy.ResolveBrowseMinVoteCount(
            DiscoverBrowseMode.Trending,
            DiscoverBrowseSort.PopularityDesc,
            null,
            newReleasesVoteFloor: 75);

        Assert.Null(effective);
    }
}
