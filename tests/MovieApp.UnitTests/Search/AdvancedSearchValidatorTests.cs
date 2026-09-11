using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class AdvancedSearchValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void ValidateQueryFailsForShortQuery(string query)
    {
        var result = AdvancedSearchValidator.ValidateQuery(query, required: true);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateQueryAllowsEmptyWhenNotRequired()
    {
        var result = AdvancedSearchValidator.ValidateQuery(null);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateQueryFailsForTooLongQuery()
    {
        var result = AdvancedSearchValidator.ValidateQuery(new string('a', 101), required: true);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePaginationFailsForInvalidPage()
    {
        var result = AdvancedSearchValidator.ValidatePagination(0, 20);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePaginationFailsForInvalidPageSize()
    {
        var result = AdvancedSearchValidator.ValidatePagination(1, 101);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateTypeFailsForInvalidValue()
    {
        var result = AdvancedSearchValidator.ValidateType("invalid");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateSortFailsForInvalidValue()
    {
        var result = AdvancedSearchValidator.ValidateSort("newest");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRatingsFailsWhenMinGreaterThanMax()
    {
        var result = AdvancedSearchValidator.ValidateRatings(8m, 5m);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsValidCriteria()
    {
        var criteria = new SearchCriteria("batman", SearchContentType.All, null, 2020, 5m, 9m, SearchSortOption.Relevance, 1, 20);
        var result = AdvancedSearchValidator.Validate(criteria);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("movie", SearchContentType.Movie)]
    [InlineData("tv", SearchContentType.Tv)]
    [InlineData("all", SearchContentType.All)]
    public void TryParseTypeParsesKnownValues(string value, SearchContentType expected)
    {
        var parsed = AdvancedSearchValidator.TryParseType(value, out var contentType);
        Assert.True(parsed);
        Assert.Equal(expected, contentType);
    }

    [Theory]
    [InlineData("rating", SearchSortOption.RatingDesc)]
    [InlineData("rating_desc", SearchSortOption.RatingDesc)]
    [InlineData("popular", SearchSortOption.Popular)]
    public void TryParseSortNormalizesValues(string value, SearchSortOption expected)
    {
        var parsed = AdvancedSearchValidator.TryParseSort(value, out var sortOption);
        Assert.True(parsed);
        Assert.Equal(expected, sortOption);
    }
}
