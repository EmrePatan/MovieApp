using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class WatchlistItemTests
{
    [Fact]
    public void CreateForMovieSetsExpectedValues()
    {
        var watchlistId = Guid.NewGuid();
        var movieId = Guid.NewGuid();

        var item = WatchlistItem.CreateForMovie(watchlistId, movieId, DateTime.UtcNow);

        Assert.Equal(watchlistId, item.WatchlistId);
        Assert.Equal(movieId, item.MovieId);
        Assert.Null(item.TvShowId);
        item.ValidateInvariants();
    }

    [Fact]
    public void CreateForTvShowSetsExpectedValues()
    {
        var watchlistId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();

        var item = WatchlistItem.CreateForTvShow(watchlistId, tvShowId, DateTime.UtcNow);

        Assert.Equal(watchlistId, item.WatchlistId);
        Assert.Equal(tvShowId, item.TvShowId);
        Assert.Null(item.MovieId);
        item.ValidateInvariants();
    }

    [Fact]
    public void ValidateInvariantsThrowsWhenBothReferencesAreSet()
    {
        var item = WatchlistItem.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        item.TvShowId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => item.ValidateInvariants());
    }
}
