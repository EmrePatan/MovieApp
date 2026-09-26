using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiRequestedPersonSearchQueryExtractorTests
{
    [Theory]
    [InlineData("Bensu Soral dizisi veya bi filmini öner", "Bensu Soral")]
    [InlineData("Cem Yılmaz filmi öner", "Cem Yılmaz")]
    [InlineData("bana Nurgül Yeşilçay dizisini öner", "Nurgül Yeşilçay")]
    public void TryExtractReturnsPersonNameForActorCentricPrompts(string message, string expected)
    {
        var query = AiRequestedPersonSearchQueryExtractor.TryExtract(message);
        Assert.Equal(expected, query);
    }

    [Theory]
    [InlineData("korku filmi öner")]
    [InlineData("something moody tonight")]
    [InlineData("")]
    public void TryExtractReturnsNullForGenericPrompts(string message)
    {
        Assert.Null(AiRequestedPersonSearchQueryExtractor.TryExtract(message));
    }
}
