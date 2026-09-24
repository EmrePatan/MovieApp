using MovieApp.Application.Services.ExternalRatings;

namespace MovieApp.UnitTests.ExternalRatings;

public sealed class ExternalRatingSourceNormalizerTests
{
    [Theory]
    [InlineData("tomatoes", 93, "tomatometer", 100)]
    [InlineData("tomatoesaudience", 89, "popcornmeter", 100)]
    [InlineData("popcorn", 75, "popcornmeter", 100)]
    [InlineData("imdb", 8.4, "imdb", 10)]
    [InlineData("letterboxd", 4.2, "letterboxd", 5)]
    [InlineData("metacritic", 82, "metacritic", 100)]
    public void TryNormalize_MapsKnownSources(string raw, decimal value, string expectedSource, int expectedScale)
    {
        var result = ExternalRatingSourceNormalizer.TryNormalize(raw, value, 100);

        Assert.NotNull(result);
        Assert.Equal(expectedSource, result!.Source);
        Assert.Equal(value, result.Value);
        Assert.Equal(expectedScale, result.Scale);
    }

    [Fact]
    public void TryNormalize_RejectsOutOfRangeValues()
    {
        Assert.Null(ExternalRatingSourceNormalizer.TryNormalize("imdb", 11, null));
        Assert.Null(ExternalRatingSourceNormalizer.TryNormalize("letterboxd", -1, null));
    }

    [Fact]
    public void TryNormalize_IgnoresUnknownSources()
    {
        Assert.Null(ExternalRatingSourceNormalizer.TryNormalize("trakt", 8, null));
    }
}
