using MovieApp.Application.Common;

namespace MovieApp.UnitTests.Common;

public sealed class QueryNormalizerTests
{
    [Theory]
    [InlineData("Interstellar", "interstellar")]
    [InlineData(" INTERSTELLAR ", "interstellar")]
    [InlineData("InTeRsTeLLaR", "interstellar")]
    public void NormalizeTrimsAndLowercasesQuery(string input, string expected)
    {
        Assert.Equal(expected, QueryNormalizer.Normalize(input));
    }
}
