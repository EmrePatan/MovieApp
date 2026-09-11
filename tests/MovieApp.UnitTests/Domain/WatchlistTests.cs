using MovieApp.Domain.Entities;
using MovieApp.Domain.Watchlists;

namespace MovieApp.UnitTests.Domain;

public sealed class WatchlistTests
{
    [Fact]
    public void CreateTrimsNameAndNormalizes()
    {
        var watchlist = Watchlist.Create(Guid.NewGuid(), "  Weekend Watch  ", DateTime.UtcNow);

        Assert.Equal("Weekend Watch", watchlist.Name);
        Assert.Equal(WatchlistNameNormalizer.Normalize("Weekend Watch"), watchlist.NormalizedName);
    }

    [Fact]
    public void CreateThrowsWhenNameIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            Watchlist.Create(Guid.NewGuid(), "   ", DateTime.UtcNow));
    }

    [Fact]
    public void CreateThrowsWhenNameExceedsMaxLength()
    {
        var longName = new string('a', WatchlistNameNormalizer.MaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            Watchlist.Create(Guid.NewGuid(), longName, DateTime.UtcNow));
    }
}
