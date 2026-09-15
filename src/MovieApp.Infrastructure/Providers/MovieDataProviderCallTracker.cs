namespace MovieApp.Infrastructure.Providers;

public sealed class MovieDataProviderCallTracker
{
    private int _searchMoviesCallCount;
    private int _discoverMoviesCallCount;

    public int SearchMoviesCallCount => _searchMoviesCallCount;

    public int DiscoverMoviesCallCount => _discoverMoviesCallCount;

    public bool FailDiscoverMovies { get; set; }

    public void RecordSearchMovies() => Interlocked.Increment(ref _searchMoviesCallCount);

    public void RecordDiscoverMovies() => Interlocked.Increment(ref _discoverMoviesCallCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _searchMoviesCallCount, 0);
        Interlocked.Exchange(ref _discoverMoviesCallCount, 0);
        FailDiscoverMovies = false;
    }
}
