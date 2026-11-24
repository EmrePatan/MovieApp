using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class AdvancedDiscoverSortTests
{
    [Theory]
    [InlineData("popularity_desc", AdvancedDiscoverSort.PopularityDesc)]
    [InlineData("popularity_asc", AdvancedDiscoverSort.PopularityAsc)]
    [InlineData("rating_desc", AdvancedDiscoverSort.RatingDesc)]
    [InlineData("rating_asc", AdvancedDiscoverSort.RatingAsc)]
    [InlineData("newest", AdvancedDiscoverSort.Newest)]
    [InlineData("oldest", AdvancedDiscoverSort.Oldest)]
    [InlineData("title_asc", AdvancedDiscoverSort.TitleAsc)]
    [InlineData("TITLE_DESC", AdvancedDiscoverSort.TitleDesc)]
    public void TryParseSort_AcceptsEveryBrowseStyleOption(string value, AdvancedDiscoverSort expected)
    {
        Assert.True(AdvancedDiscoverValidator.TryParseSort(value, out var sort));
        Assert.Equal(expected, sort);
    }

    [Fact]
    public void TryParseSort_StillRejectsUnknownValues()
    {
        Assert.False(AdvancedDiscoverValidator.TryParseSort("random", out _));
    }

    [Fact]
    public void RatingAscending_GetsTheSameVoteFloorsAsRatingDescending()
    {
        Assert.True(DiscoverCatalogSortPolicy.RequiresRatingVoteFloor(AdvancedDiscoverSort.RatingAsc));
        Assert.Equal(
            DiscoverCatalogSortPolicy.ResolveAdvancedMinVoteCount(AdvancedDiscoverSort.RatingDesc, null, null),
            DiscoverCatalogSortPolicy.ResolveAdvancedMinVoteCount(AdvancedDiscoverSort.RatingAsc, null, null));
        Assert.Equal(
            WorldCinemaDiscoverQuality.ResolveMinimumVoteCount(SearchContentType.Movie, AdvancedDiscoverSort.RatingDesc),
            WorldCinemaDiscoverQuality.ResolveMinimumVoteCount(SearchContentType.Movie, AdvancedDiscoverSort.RatingAsc));
    }
}
