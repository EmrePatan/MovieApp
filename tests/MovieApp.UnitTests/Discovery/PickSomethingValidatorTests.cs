using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Discovery;

public sealed class PickSomethingValidatorTests
{
    [Fact]
    public void ParseSessionExcludedIdsAcceptsCommaSeparatedAndRepeatedValues()
    {
        var firstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var parsed = PickSomethingValidator.ParseSessionExcludedIds(
        [
            $"{firstId},{secondId}",
            firstId.ToString()
        ]);

        Assert.Equal(2, parsed.Count);
        Assert.Contains(firstId, parsed);
        Assert.Contains(secondId, parsed);
    }

    [Fact]
    public void ValidateRejectsTooManySessionExcludedIds()
    {
        var excludedIds = Enumerable.Range(0, PickSomethingValidator.MaxSessionExcludedIds + 1)
            .Select(_ => Guid.NewGuid())
            .ToHashSet();

        var result = PickSomethingValidator.Validate(
            new PickSomethingCriteria(
                Application.Models.Recommendations.RecommendationContentType.All,
                excludedIds));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("movie")]
    [InlineData("tv")]
    [InlineData("all")]
    [InlineData(null)]
    public void ValidateMediaTypeAcceptsSupportedValues(string? mediaType)
    {
        var result = PickSomethingValidator.ValidateMediaType(mediaType);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateMediaTypeRejectsUnknownValue()
    {
        var result = PickSomethingValidator.ValidateMediaType("person");

        Assert.False(result.IsValid);
    }
}
