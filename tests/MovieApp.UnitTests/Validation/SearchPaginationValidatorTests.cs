using MovieApp.Application.Models.Movies;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class SearchPaginationValidatorTests
{
    [Fact]
    public void ValidateAcceptsDefaultPaginationValues()
    {
        var result = SearchPaginationValidator.Validate(
            MovieSearchPagination.DefaultPage,
            MovieSearchPagination.DefaultPageSize);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsMaximumPageSize()
    {
        var result = SearchPaginationValidator.Validate(1, MovieSearchPagination.MaxPageSize);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateRejectsNonPositivePage(int page)
    {
        var result = SearchPaginationValidator.Validate(page, MovieSearchPagination.DefaultPageSize);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsPageWhoseOffsetFitsInInt()
    {
        var result = SearchPaginationValidator.Validate(int.MaxValue, 1);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsPageWhoseOffsetOverflowsInt()
    {
        var result = SearchPaginationValidator.Validate(int.MaxValue, 2);

        Assert.False(result.IsValid);
        Assert.Contains("too large", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void ValidateRejectsInvalidPageSize(int pageSize)
    {
        var result = SearchPaginationValidator.Validate(MovieSearchPagination.DefaultPage, pageSize);

        Assert.False(result.IsValid);
    }
}
