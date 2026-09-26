using MovieApp.Application.Common;

namespace MovieApp.UnitTests.Common;

public sealed class QueryNormalizerTests
{
    [Theory]
    [InlineData("Interstellar", "Interstellar")]
    [InlineData(" INTERSTELLAR ", "INTERSTELLAR")]
    [InlineData("InTeRsTeLLaR", "InTeRsTeLLaR")]
    [InlineData("  çok   güzel  ", "çok güzel")]
    public void NormalizeTrimsCollapsesWhitespaceAndAppliesNfcWithoutChangingCase(string input, string expected)
    {
        Assert.Equal(expected, QueryNormalizer.Normalize(input));
    }
}
