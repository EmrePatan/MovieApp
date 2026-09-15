using MovieApp.Application.Services.Movies;

namespace MovieApp.UnitTests.Movies;

public sealed class MovieConsumptionReleaseGuardrailTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    [Fact]
    public void IsReleasedForConsumption_FutureEffectiveDate_ReturnsFalse()
    {
        Assert.False(MovieConsumptionReleaseGuardrail.IsReleasedForConsumption(Today.AddDays(1), Today));
    }

    [Fact]
    public void IsReleasedForConsumption_PastEffectiveDate_ReturnsTrue()
    {
        Assert.True(MovieConsumptionReleaseGuardrail.IsReleasedForConsumption(Today.AddDays(-1), Today));
    }

    [Fact]
    public void IsReleasedForConsumption_TodayEffectiveDate_ReturnsTrue()
    {
        Assert.True(MovieConsumptionReleaseGuardrail.IsReleasedForConsumption(Today, Today));
    }

    [Fact]
    public void IsReleasedForConsumption_NullEffectiveDate_ReturnsTrue()
    {
        Assert.True(MovieConsumptionReleaseGuardrail.IsReleasedForConsumption(null, Today));
    }
}
