using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverBrowseValidatorTests
{
    [Theory]
    [InlineData("trending", DiscoverBrowseMode.Trending)]
    [InlineData("top_rated", DiscoverBrowseMode.TopRated)]
    [InlineData("new_releases", DiscoverBrowseMode.NewReleases)]
    public void TryParseModeParsesSupportedValues(string rawMode, DiscoverBrowseMode expectedMode)
    {
        var parsed = DiscoverBrowseValidator.TryParseMode(rawMode, out var mode);

        Assert.True(parsed);
        Assert.Equal(expectedMode, mode);
    }

    [Theory]
    [InlineData("popularity_desc", DiscoverBrowseSort.PopularityDesc)]
    [InlineData("rating_desc", DiscoverBrowseSort.RatingDesc)]
    [InlineData("release_desc", DiscoverBrowseSort.ReleaseDesc)]
    [InlineData("title_asc", DiscoverBrowseSort.TitleAsc)]
    public void TryParseSortParsesSupportedValues(string rawSort, DiscoverBrowseSort expectedSort)
    {
        var parsed = DiscoverBrowseValidator.TryParseSort(rawSort, out var sort);

        Assert.True(parsed);
        Assert.Equal(expectedSort, sort);
    }

    [Fact]
    public void ParseGenreIdsCombinesRepeatableAndCommaSeparatedValues()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var parsed = DiscoverBrowseValidator.ParseGenreIds(
            [$"{first},{second}", first.ToString()],
            null);

        Assert.Equal(2, parsed.Count);
        Assert.Contains(first, parsed);
        Assert.Contains(second, parsed);
    }

    [Fact]
    public void ValidateRejectsInvalidMinRating()
    {
        var criteria = CreateCriteria(minRating: 11m);

        var validation = DiscoverBrowseValidator.Validate(criteria);

        Assert.False(validation.IsValid);
    }

    [Theory]
    [InlineData(DiscoverBrowseMode.Trending, DiscoverBrowseSort.PopularityDesc)]
    [InlineData(DiscoverBrowseMode.TopRated, DiscoverBrowseSort.RatingDesc)]
    [InlineData(DiscoverBrowseMode.NewReleases, DiscoverBrowseSort.ReleaseDesc)]
    public void GetDefaultSortForModeMatchesBrowseContract(
        DiscoverBrowseMode mode,
        DiscoverBrowseSort expectedSort)
    {
        Assert.Equal(expectedSort, DiscoverBrowseValidator.GetDefaultSortForMode(mode));
    }

    private static DiscoverBrowseCriteria CreateCriteria(decimal? minRating = null) =>
        new(
            DiscoverBrowseMode.Trending,
            SearchContentType.All,
            [],
            null,
            minRating,
            null,
            null,
            1,
            20);
}
