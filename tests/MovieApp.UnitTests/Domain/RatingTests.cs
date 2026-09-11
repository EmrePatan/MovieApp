using MovieApp.Domain.Entities;
using MovieApp.Domain.Ratings;

namespace MovieApp.UnitTests.Domain;

public sealed class RatingTests
{
    [Fact]
    public void CreateForMovieAcceptsScoreOne()
    {
        var rating = Rating.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow);
        Assert.Equal(1, rating.Score);
    }

    [Fact]
    public void CreateForMovieAcceptsScoreTen()
    {
        var rating = Rating.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), 10, DateTime.UtcNow);
        Assert.Equal(10, rating.Score);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public void CreateForMovieRejectsInvalidScore(int score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Rating.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), score, DateTime.UtcNow));
    }

    [Fact]
    public void ValidateInvariantsRejectsBothCatalogReferences()
    {
        var rating = Rating.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), 8, DateTime.UtcNow);
        rating.TvShowId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => rating.ValidateInvariants());
    }

    [Fact]
    public void ValidateInvariantsRejectsMissingCatalogReference()
    {
        var rating = new Rating
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Score = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() => rating.ValidateInvariants());
    }

    [Fact]
    public void CreateForTvShowCreatesValidRating()
    {
        var rating = Rating.CreateForTvShow(Guid.NewGuid(), Guid.NewGuid(), 7, DateTime.UtcNow);

        Assert.Null(rating.MovieId);
        Assert.NotNull(rating.TvShowId);
        rating.ValidateInvariants();
    }

    [Fact]
    public void UpdateScoreUpdatesTimestamp()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var rating = Rating.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), 5, createdAt);
        var updatedAt = DateTime.UtcNow;

        rating.UpdateScore(9, updatedAt);

        Assert.Equal(9, rating.Score);
        Assert.Equal(updatedAt, rating.UpdatedAt);
    }
}
