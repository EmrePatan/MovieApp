using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class WatchedMovieTests
{
    [Fact]
    public void CreateSetsRequiredFields()
    {
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        var watchedMovie = WatchedMovie.Create(userId, movieId, utcNow);

        Assert.NotEqual(Guid.Empty, watchedMovie.Id);
        Assert.Equal(userId, watchedMovie.UserId);
        Assert.Equal(movieId, watchedMovie.MovieId);
        Assert.Equal(utcNow, watchedMovie.WatchedAt);
        Assert.Equal(utcNow, watchedMovie.CreatedAt);
        Assert.Equal(utcNow, watchedMovie.UpdatedAt);
    }

    [Fact]
    public void UpdateWatchedAtUpdatesTimestamps()
    {
        var watchedMovie = WatchedMovie.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(-1));
        var updatedAt = DateTime.UtcNow;

        watchedMovie.UpdateWatchedAt(updatedAt);

        Assert.Equal(updatedAt, watchedMovie.WatchedAt);
        Assert.Equal(updatedAt, watchedMovie.UpdatedAt);
    }

    [Fact]
    public void CreateThrowsWhenUserIdEmpty()
    {
        Assert.Throws<ArgumentException>(() => WatchedMovie.Create(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void CreateThrowsWhenMovieIdEmpty()
    {
        Assert.Throws<ArgumentException>(() => WatchedMovie.Create(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow));
    }
}
