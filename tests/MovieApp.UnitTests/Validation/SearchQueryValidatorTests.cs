using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class SearchQueryValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRejectsMissingQuery(string? query)
    {
        var result = SearchQueryValidator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Equal("Search query is required.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateRejectsTooShortQuery()
    {
        var result = SearchQueryValidator.Validate("a");

        Assert.False(result.IsValid);
        Assert.Contains("at least 2 characters", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsTooLongQuery()
    {
        var result = SearchQueryValidator.Validate(new string('a', 101));

        Assert.False(result.IsValid);
        Assert.Contains("must not exceed 100 characters", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAcceptsValidQuery()
    {
        var result = SearchQueryValidator.Validate("Interstellar");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }
}
