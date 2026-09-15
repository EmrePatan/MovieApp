using MovieApp.Application.Caching;

namespace MovieApp.UnitTests.Caching;

public sealed class CreditsCacheKeysTests
{
    [Fact]
    public void MovieCreditsCacheKeyUsesV2Format()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var key = MovieCreditsCacheKeys.Create(movieId);

        Assert.Equal($"movie-credits:{movieId}:v2", key);
        Assert.Equal("v2", MovieCreditsCacheKeys.Version);
    }

    [Fact]
    public void TvShowCreditsCacheKeyUsesV2Format()
    {
        var tvShowId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var key = TvShowCreditsCacheKeys.Create(tvShowId);

        Assert.Equal($"tvshow-credits:{tvShowId}:v2", key);
        Assert.Equal("v2", TvShowCreditsCacheKeys.Version);
    }
}
