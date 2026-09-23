using MovieApp.Domain.Entities;
using MovieApp.Domain.Reviews;

namespace MovieApp.UnitTests.Domain;

public sealed class ReviewTests
{
    [Fact]
    public void CreateForMovieTrimsContent()
    {
        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "  Great movie  ", "en-US", DateTime.UtcNow);

        Assert.Equal("Great movie", review.Content);
    }

    [Fact]
    public void CreateForMovieAcceptsEmojiOnlyContent()
    {
        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "🔥👍", "en-US", DateTime.UtcNow);

        Assert.Equal("🔥👍", review.Content);
        review.ValidateInvariants();
    }

    [Fact]
    public void CreateForMovieRejectsEmptyContent()
    {
        Assert.Throws<ArgumentException>(() =>
            Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "   ", null, DateTime.UtcNow));
    }

    [Fact]
    public void CreateForMovieRejectsContentLongerThanMaxLength()
    {
        var content = new string('a', ReviewContentRules.MaxLength + 1);

        Assert.Throws<ArgumentException>(() =>
            Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), content, null, DateTime.UtcNow));
    }

    [Fact]
    public void CreateForMovieAcceptsContentAtMaxLength()
    {
        var content = new string('a', ReviewContentRules.MaxLength);

        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), content, "en-US", DateTime.UtcNow);

        Assert.Equal(ReviewContentRules.MaxLength, review.Content.Length);
    }

    [Fact]
    public void ValidateInvariantsRejectsBothCatalogReferences()
    {
        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "Valid", "en-US", DateTime.UtcNow);
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
    public void UpdateContentUpdatesAuthoringLocale()
    {
        var review = Review.CreateForMovie(Guid.NewGuid(), Guid.NewGuid(), "Original", "en-US", DateTime.UtcNow);

        review.UpdateContent("Updated", "tr-TR", DateTime.UtcNow.AddMinutes(1));

        Assert.Equal("Updated", review.Content);
        Assert.Equal("tr-TR", review.AuthoringLocale);
    }

    [Fact]
    public void CreateForTvShowCreatesValidReview()
    {
        var review = Review.CreateForTvShow(Guid.NewGuid(), Guid.NewGuid(), "Great show", "en-US", DateTime.UtcNow);

        Assert.Null(review.MovieId);
        Assert.NotNull(review.TvShowId);
        review.ValidateInvariants();
    }
}
