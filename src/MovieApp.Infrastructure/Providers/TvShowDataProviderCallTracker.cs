namespace MovieApp.Infrastructure.Providers;

public sealed class TvShowDataProviderCallTracker
{
    private int _searchTvShowsCallCount;
    private int _getTvShowCallCount;
    private int _getSeasonCallCount;
    private int _discoverTvShowsCallCount;

    public int SearchTvShowsCallCount => _searchTvShowsCallCount;

    public int GetTvShowCallCount => _getTvShowCallCount;

    public int GetSeasonCallCount => _getSeasonCallCount;

    public int DiscoverTvShowsCallCount => _discoverTvShowsCallCount;

    public bool FailGetTvShow { get; set; }

    public bool FailGetSeason { get; set; }

    public bool FailDiscoverTvShows { get; set; }

    public void RecordSearchTvShows() => Interlocked.Increment(ref _searchTvShowsCallCount);

    public void RecordGetTvShow() => Interlocked.Increment(ref _getTvShowCallCount);

    public void RecordGetSeason() => Interlocked.Increment(ref _getSeasonCallCount);

    public void RecordDiscoverTvShows() => Interlocked.Increment(ref _discoverTvShowsCallCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _searchTvShowsCallCount, 0);
        Interlocked.Exchange(ref _getTvShowCallCount, 0);
        Interlocked.Exchange(ref _getSeasonCallCount, 0);
        Interlocked.Exchange(ref _discoverTvShowsCallCount, 0);
        FailGetTvShow = false;
        FailGetSeason = false;
        FailDiscoverTvShows = false;
    }
}
