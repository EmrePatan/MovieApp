using MovieApp.Application.Caching;

namespace MovieApp.UnitTests.Caching;

public sealed class WatchProvidersCacheKeysTests
{
    [Fact]
    public void MovieWatchProvidersCacheKeyIncludesVersionV2()
    {
        var movieId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        var key = MovieWatchProvidersCacheKeys.Create(movieId, "tr");

        Assert.Equal(
            $"movie-watch-providers:{movieId}:TR:{MovieWatchProvidersCacheKeys.Version}",
            key);
        Assert.EndsWith(":v2", key);
    }

    [Fact]
    public void TvShowWatchProvidersCacheKeyIncludesVersionV2()
    {
        var tvShowId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        var key = TvShowWatchProvidersCacheKeys.Create(tvShowId, "de");

        Assert.Equal(
            $"tvshow-watch-providers:{tvShowId}:DE:{TvShowWatchProvidersCacheKeys.Version}",
            key);
        Assert.EndsWith(":v2", key);
    }
}
