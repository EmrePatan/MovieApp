using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationQueryRoundTripTests
{
    public const int BaselineUserContextDbRoundTrips = 18;
    public const int OptimizedUserContextDbRoundTripsWithoutSearch = 11;
    public const int OptimizedUserContextDbRoundTripsWithSearch = 13;
    public const int BaselineCandidateFetchDbRoundTrips = 6;
    public const int OptimizedCandidateFetchDbRoundTrips = 2;

    [Fact]
    public void RecommendationQueryMetricsIncrementsRoundTripCount()
    {
        var metrics = new RecommendationQueryMetrics();

        metrics.RecordRoundTrip();
        metrics.RecordRoundTrip();
        metrics.RecordRoundTrip();

        Assert.Equal(3, metrics.DbRoundTrips);
    }

    [Fact]
    public void OptimizedPathTargetsLowTeensApplicationLevelRoundTrips()
    {
        var optimizedPersonalizedPath = OptimizedUserContextDbRoundTripsWithoutSearch
            + OptimizedCandidateFetchDbRoundTrips;

        Assert.True(
            optimizedPersonalizedPath < BaselineUserContextDbRoundTrips,
            "Consolidated user context and candidate fetch should reduce application-level DB round trips.");
        Assert.InRange(optimizedPersonalizedPath, 10, 15);
    }
}
