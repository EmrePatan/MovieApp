namespace MovieApp.Infrastructure.Persistence.Repositories;

internal sealed class RecommendationQueryMetrics
{
    private int _dbRoundTrips;

    public int DbRoundTrips => _dbRoundTrips;

    public void RecordRoundTrip() => Interlocked.Increment(ref _dbRoundTrips);
}
