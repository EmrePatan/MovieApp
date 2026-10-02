using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class HiddenGemsPolicyTests
{
    [Theory]
    [InlineData(7.5, 75, "/poster.jpg", true)]
    [InlineData(7.5, 2000, "/poster.jpg", true)]
    [InlineData(7.49, 200, "/poster.jpg", false)]
    [InlineData(8.0, 74, "/poster.jpg", false)]
    [InlineData(8.0, 2001, "/poster.jpg", false)]
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
    [InlineData(7.5, 40, "/poster.jpg", true)]
    [InlineData(7.5, 1500, "/poster.jpg", true)]
    [InlineData(8.0, 39, "/poster.jpg", false)]
    [InlineData(8.0, 1501, "/poster.jpg", false)]
    [InlineData(9.0, 100, null, false)]
    public void TvWindowRequiresRatingVotesAndPoster(
        decimal voteAverage,
        int voteCount,
        string? posterPath,
        bool expected)
    {
        Assert.Equal(expected, HiddenGemsPolicy.IsTvHiddenGem(voteAverage, voteCount, posterPath));
    }
}
