using MovieApp.Application.Common;

namespace MovieApp.UnitTests.Common;

public sealed class ImdbIdNormalizerTests
{
    [Fact]
    public void NormalizeReturnsNullForNull()
    {
        Assert.Null(ImdbIdNormalizer.Normalize(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void NormalizeReturnsNullForEmptyOrWhitespace(string imdbId)
    {
        Assert.Null(ImdbIdNormalizer.Normalize(imdbId));
    }

    [Fact]
    public void NormalizePreservesValidImdbId()
    {
        Assert.Equal("tt1375666", ImdbIdNormalizer.Normalize("tt1375666"));
    }

    [Fact]
    public void NormalizeTrimsValidImdbId()
    {
        Assert.Equal("tt1375666", ImdbIdNormalizer.Normalize("  tt1375666  "));
    }
}
