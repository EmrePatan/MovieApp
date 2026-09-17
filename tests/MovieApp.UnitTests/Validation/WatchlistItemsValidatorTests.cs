using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class WatchlistItemsValidatorTests
{
    [Theory]
    [InlineData("all", true)]
    [InlineData("movie", true)]
    [InlineData("tv", true)]
    [InlineData("person", false)]
    [InlineData("invalid", false)]
    public void ValidateMediaType_accepts_supported_values(string mediaType, bool expectedValid)
    {
        var result = WatchlistItemsValidator.ValidateMediaType(mediaType);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("recentlyAdded", WatchlistItemsSort.RecentlyAdded)]
    [InlineData("titleAsc", WatchlistItemsSort.TitleAsc)]
    [InlineData("ratingDesc", WatchlistItemsSort.RatingDesc)]
    public void TryParseSort_parses_supported_values(string sort, WatchlistItemsSort expected)
    {
        Assert.True(WatchlistItemsValidator.TryParseSort(sort, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void ValidateSort_rejects_unknown_values()
    {
        var result = WatchlistItemsValidator.ValidateSort("newest");
        Assert.False(result.IsValid);
    }
}
