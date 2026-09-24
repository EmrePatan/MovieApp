using MovieApp.Application.Services.ExternalRatings;

namespace MovieApp.UnitTests.ExternalRatings;

public sealed class ExternalRatingsCachePolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_ReturnsFreshWithinFreshWindow()
    {
        var fetchedAt = Now.AddHours(-48);
        var state = ExternalRatingsCachePolicy.Evaluate(
            fetchedAt,
            isNegative: false,
            freshWindow: TimeSpan.FromHours(72),
            staleWindow: TimeSpan.FromDays(14),
            negativeWindow: TimeSpan.FromHours(24),
            Now);

        Assert.Equal(ExternalRatingsCacheState.Fresh, state);
    }

    [Fact]
    public void Evaluate_ReturnsStaleUsableBetweenFreshAndStaleWindows()
    {
        var fetchedAt = Now.AddDays(-5);
        var state = ExternalRatingsCachePolicy.Evaluate(
            fetchedAt,
            isNegative: false,
            freshWindow: TimeSpan.FromHours(72),
            staleWindow: TimeSpan.FromDays(14),
            negativeWindow: TimeSpan.FromHours(24),
            Now);

        Assert.Equal(ExternalRatingsCacheState.StaleUsable, state);
    }

    [Fact]
    public void Evaluate_ReturnsNegativeFreshWithinNegativeWindow()
    {
        var fetchedAt = Now.AddHours(-12);
        var state = ExternalRatingsCachePolicy.Evaluate(
            fetchedAt,
            isNegative: true,
            freshWindow: TimeSpan.FromHours(72),
            staleWindow: TimeSpan.FromDays(14),
            negativeWindow: TimeSpan.FromHours(24),
            Now);

        Assert.Equal(ExternalRatingsCacheState.NegativeFresh, state);
    }

    [Fact]
    public void Evaluate_ReturnsExpiredAfterStaleWindow()
    {
        var fetchedAt = Now.AddDays(-20);
        var state = ExternalRatingsCachePolicy.Evaluate(
            fetchedAt,
            isNegative: false,
            freshWindow: TimeSpan.FromHours(72),
            staleWindow: TimeSpan.FromDays(14),
            negativeWindow: TimeSpan.FromHours(24),
            Now);

        Assert.Equal(ExternalRatingsCacheState.Expired, state);
    }
}
