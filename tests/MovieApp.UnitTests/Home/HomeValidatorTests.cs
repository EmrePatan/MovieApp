using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Home;

public sealed class HomeValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    public void ValidateSectionSizeAcceptsValidValues(int sectionSize)
    {
        var result = HomeValidator.ValidateSectionSize(sectionSize, 1, 20);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void ValidateSectionSizeRejectsInvalidValues(int sectionSize)
    {
        var result = HomeValidator.ValidateSectionSize(sectionSize, 1, 20);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("movie")]
    [InlineData("tv")]
    [InlineData(null)]
    public void ValidateTypeAcceptsValidValues(string? type)
    {
        var result = HomeValidator.ValidateType(type);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTypeRejectsInvalidValue()
    {
        var result = HomeValidator.ValidateType("invalid");

        Assert.False(result.IsValid);
    }
}
