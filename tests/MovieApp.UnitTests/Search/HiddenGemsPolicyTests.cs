using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class HiddenGemsPolicyTests
{
    [Theory]
    [InlineData(7.5, 100, "/poster.jpg", true)]
    [InlineData(7.5, 800, "/poster.jpg", true)]
    [InlineData(7.49, 200, "/poster.jpg", false)]
    [InlineData(8.0, 99, "/poster.jpg", false)]
    [InlineData(8.0, 801, "/poster.jpg", false)]
    [InlineData(8.0, 200, " ", false)]
    public void MovieWindowRequiresRatingVotesAndPoster(
        decimal voteAverage,
        int voteCount,
        string posterPath,
        bool expected)
    {
        Assert.Equal(expected, HiddenGemsPolicy.IsMovieHiddenGem(voteAverage, voteCount, posterPath));
    }

    [Theory]
    [InlineData(7.5, 50, "/poster.jpg", true)]
    [InlineData(7.5, 850, "/poster.jpg", true)]
    [InlineData(8.0, 49, "/poster.jpg", false)]
    [InlineData(8.0, 851, "/poster.jpg", false)]
    [InlineData(9.0, 100, null, false)]
    public void TvWindowRequiresRatingVotesAndPoster(
        decimal voteAverage,
        int voteCount,
        string? posterPath,
        bool expected)
    {
        Assert.Equal(expected, HiddenGemsPolicy.IsTvHiddenGem(voteAverage, voteCount, posterPath));
    }

    [Fact]
    public void MinimumVoteConfidenceMatchesBayesianPrior()
    {
        Assert.Equal(300, HiddenGemsPolicy.MinimumVoteConfidence);
    }
}
