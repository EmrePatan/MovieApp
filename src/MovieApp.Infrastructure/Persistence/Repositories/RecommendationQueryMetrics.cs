namespace MovieApp.Infrastructure.Persistence.Repositories;

internal sealed class RecommendationQueryMetrics
{
    public int DbRoundTrips { get; private set; }

    public void RecordRoundTrip() => DbRoundTrips++;
}
