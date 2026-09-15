using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class AdvancedDiscoverValidatorTests
{
    [Theory]
    [InlineData("popularity_desc", AdvancedDiscoverSort.PopularityDesc)]
    [InlineData("rating_desc", AdvancedDiscoverSort.RatingDesc)]
    [InlineData("newest", AdvancedDiscoverSort.Newest)]
    [InlineData("oldest", AdvancedDiscoverSort.Oldest)]
    public void TryParseSortParsesSupportedValues(string rawSort, AdvancedDiscoverSort expectedSort)
    {
        var parsed = AdvancedDiscoverValidator.TryParseSort(rawSort, out var sort);

        Assert.True(parsed);
        Assert.Equal(expectedSort, sort);
    }

    [Theory]
    [InlineData("movie")]
    [InlineData("tv")]
    public void ValidateMediaTypeAcceptsMovieAndTv(string mediaType)
    {
        var validation = AdvancedDiscoverValidator.ValidateMediaType(mediaType);

        Assert.True(validation.IsValid);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("person")]
    [InlineData("")]
    public void ValidateMediaTypeRejectsUnsupportedValues(string mediaType)
    {
        var validation = AdvancedDiscoverValidator.ValidateMediaType(mediaType);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateRejectsYearAndYearRangeTogether()
    {
        var criteria = CreateCriteria(year: 2024, yearFrom: 2020, yearTo: 2024);

        var validation = AdvancedDiscoverValidator.Validate(criteria);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidOriginCountry()
    {
        var criteria = CreateCriteria(originCountry: "USA");

        var validation = AdvancedDiscoverValidator.Validate(criteria);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateAcceptsValidOriginCountry()
    {
        var criteria = CreateCriteria(originCountry: "us");

        var validation = AdvancedDiscoverValidator.Validate(criteria);

        Assert.True(validation.IsValid);
    }

    [Fact]
    public void ValidateRejectsRuntimeRangeWhenMinExceedsMax()
    {
        var criteria = CreateCriteria(minRuntimeMinutes: 120, maxRuntimeMinutes: 90);

        var validation = AdvancedDiscoverValidator.Validate(criteria);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateRejectsOutOfBoundsVoteCount()
    {
        var criteria = CreateCriteria(minVoteCount: 200_000);

        var validation = AdvancedDiscoverValidator.Validate(criteria);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateRejectsOutOfBoundsPagination()
    {
        var criteria = CreateCriteria(page: 0, pageSize: 20);

        var validation = AdvancedDiscoverValidator.Validate(criteria);

        Assert.False(validation.IsValid);
    }

    private static AdvancedDiscoverCriteria CreateCriteria(
        int? year = null,
        int? yearFrom = null,
        int? yearTo = null,
        int? minRuntimeMinutes = null,
        int? maxRuntimeMinutes = null,
        int? minVoteCount = null,
        string? originCountry = null,
        int page = 1,
        int pageSize = 20) =>
        new(
            SearchContentType.Movie,
            [],
            year,
            yearFrom,
            yearTo,
            null,
            null,
            minVoteCount,
            minRuntimeMinutes,
            maxRuntimeMinutes,
            null,
            originCountry,
            null,
            [],
            [],
            AdvancedDiscoverSort.PopularityDesc,
            page,
            pageSize);
}
