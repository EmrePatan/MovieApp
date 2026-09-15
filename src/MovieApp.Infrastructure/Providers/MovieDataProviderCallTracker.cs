namespace MovieApp.Infrastructure.Providers;

public sealed class MovieDataProviderCallTracker
{
    private int _searchMoviesCallCount;
    private int _discoverMoviesCallCount;
    private int _advancedDiscoverMoviesCallCount;

    public int SearchMoviesCallCount => _searchMoviesCallCount;

    public int DiscoverMoviesCallCount => _discoverMoviesCallCount;

    public int AdvancedDiscoverMoviesCallCount => _advancedDiscoverMoviesCallCount;

    public bool FailDiscoverMovies { get; set; }

    public bool FailAdvancedDiscoverMovies { get; set; }

    public void RecordSearchMovies() => Interlocked.Increment(ref _searchMoviesCallCount);

    public void RecordDiscoverMovies() => Interlocked.Increment(ref _discoverMoviesCallCount);

    public void RecordAdvancedDiscoverMovies() => Interlocked.Increment(ref _advancedDiscoverMoviesCallCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _searchMoviesCallCount, 0);
        Interlocked.Exchange(ref _discoverMoviesCallCount, 0);
        Interlocked.Exchange(ref _advancedDiscoverMoviesCallCount, 0);
        FailDiscoverMovies = false;
        FailAdvancedDiscoverMovies = false;
    }
}
