using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationValidatorTests
{
    [Fact]
    public void ValidatePaginationRejectsInvalidPage()
    {
        var result = RecommendationValidator.ValidatePagination(0, 20);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePaginationRejectsInvalidPageSize()
    {
        var result = RecommendationValidator.ValidatePagination(1, 101);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateTypeAcceptsKnownValues()
    {
        Assert.True(RecommendationValidator.ValidateType("movie").IsValid);
        Assert.True(RecommendationValidator.ValidateType("tv").IsValid);
        Assert.True(RecommendationValidator.ValidateType("all").IsValid);
    }

    [Fact]
    public void ValidateTypeRejectsUnknownValue()
    {
        var result = RecommendationValidator.ValidateType("invalid");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TryParseTypeMapsValues()
    {
        Assert.True(RecommendationValidator.TryParseType("movie", out var movieType));
        Assert.Equal(RecommendationContentType.Movie, movieType);
    }
}
