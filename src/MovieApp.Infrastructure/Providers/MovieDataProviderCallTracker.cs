namespace MovieApp.Infrastructure.Providers;

public sealed class MovieDataProviderCallTracker
{
    private int _searchMoviesCallCount;

    public int SearchMoviesCallCount => _searchMoviesCallCount;

    public void RecordSearchMovies() => Interlocked.Increment(ref _searchMoviesCallCount);

    public void Reset() => Interlocked.Exchange(ref _searchMoviesCallCount, 0);
}
