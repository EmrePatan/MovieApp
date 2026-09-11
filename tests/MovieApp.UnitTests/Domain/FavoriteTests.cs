using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class FavoriteTests
{
    [Fact]
    public void CreateForMovieSetsExpectedValues()
    {
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        var favorite = Favorite.CreateForMovie(userId, movieId, utcNow);

        Assert.Equal(userId, favorite.UserId);
        Assert.Equal(movieId, favorite.MovieId);
        Assert.Null(favorite.TvShowId);
        Assert.Equal(utcNow, favorite.CreatedAt);
        favorite.ValidateInvariants();
    }

    [Fact]
    public void CreateForTvShowSetsExpectedValues()
    {
        var userId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        var favorite = Favorite.CreateForTvShow(userId, tvShowId, utcNow);

        Assert.Equal(userId, favorite.UserId);
        Assert.Equal(tvShowId, favorite.TvShowId);
        Assert.Null(favorite.MovieId);
        favorite.ValidateInvariants();
    }

    [Fact]
    public void ValidateInvariantsThrowsWhenBothReferencesAreSet()
    {
        var favorite = Favorite.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        favorite.TvShowId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => favorite.ValidateInvariants());
    }

    [Fact]
    public void ValidateInvariantsThrowsWhenNoReferenceIsSet()
    {
        var favorite = new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() => favorite.ValidateInvariants());
    }
}
