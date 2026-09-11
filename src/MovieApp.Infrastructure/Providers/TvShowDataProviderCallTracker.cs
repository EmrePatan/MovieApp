namespace MovieApp.Infrastructure.Providers;

public sealed class TvShowDataProviderCallTracker
{
    private int _searchTvShowsCallCount;

    public int SearchTvShowsCallCount => _searchTvShowsCallCount;

    public void RecordSearchTvShows() => Interlocked.Increment(ref _searchTvShowsCallCount);

    public void Reset() => Interlocked.Exchange(ref _searchTvShowsCallCount, 0);
}
