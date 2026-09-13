using MovieApp.Domain.Entities;
using MovieApp.Domain.Reviews;

namespace MovieApp.UnitTests.Domain;

public sealed class ReviewTests
{
    [Fact]
    public void CreateForMovieTrimsContent()
    {
        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "  Great movie  ", DateTime.UtcNow);

        Assert.Equal("Great movie", review.Content);
    }

    [Fact]
    public void CreateForMovieAcceptsEmojiOnlyContent()
    {
        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "🔥👍", DateTime.UtcNow);

        Assert.Equal("🔥👍", review.Content);
        review.ValidateInvariants();
    }

    [Fact]
    public void CreateForMovieRejectsEmptyContent()
    {
        Assert.Throws<ArgumentException>(() =>
            Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "   ", DateTime.UtcNow));
    }

    [Fact]
    public void CreateForMovieRejectsContentLongerThanMaxLength()
    {
        var content = new string('a', ReviewContentRules.MaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), content, DateTime.UtcNow));
    }

    [Fact]
    public void CreateForMovieAcceptsContentAtMaxLength()
    {
        var content = new string('a', ReviewContentRules.MaxLength);

        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), content, DateTime.UtcNow);

        Assert.Equal(ReviewContentRules.MaxLength, review.Content.Length);
    }

    [Fact]
    public void ValidateInvariantsRejectsBothCatalogReferences()
    {
        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "Valid", DateTime.UtcNow);
        review.TvShowId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => review.ValidateInvariants());
    }

    [Fact]
    public void ValidateInvariantsRejectsMissingCatalogReference()
    {
        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Content = "Valid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() => review.ValidateInvariants());
    }

    [Fact]
    public void CreateForTvShowCreatesValidReview()
    {
        var review = Review.CreateForTvShow(Guid.NewGuid(), Guid.NewGuid(), "Great show", DateTime.UtcNow);

        Assert.Null(review.MovieId);
        Assert.NotNull(review.TvShowId);
        review.ValidateInvariants();
    }
}
