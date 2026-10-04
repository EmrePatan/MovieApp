using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordSemanticEligibilityTests
{
    [Theory]
    [InlineData(KeywordClassificationStatus.Auto, true)]
    [InlineData(KeywordClassificationStatus.Approved, true)]
    [InlineData(KeywordClassificationStatus.Excluded, false)]
    public void IsRuntimeUsableMatchesClassificationPolicy(
        KeywordClassificationStatus status,
        bool expectedUsable)
    {
        Assert.Equal(expectedUsable, KeywordSemanticEligibility.IsRuntimeUsable(status));
        Assert.Equal(!expectedUsable, KeywordSemanticEligibility.IsExcluded(status));
    }
}
