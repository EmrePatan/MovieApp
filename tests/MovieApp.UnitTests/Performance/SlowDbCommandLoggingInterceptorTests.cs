using MovieApp.Infrastructure.Performance;

namespace MovieApp.UnitTests.Performance;

public sealed class SlowDbCommandLoggingInterceptorTests
{
    [Fact]
    public void Truncate_CollapsesWhitespaceAndKeepsShortText()
    {
        var result = SlowDbCommandLoggingInterceptor.Truncate("SELECT  1\r\n  FROM   t");

        Assert.Equal("SELECT 1 FROM t", result);
    }

    [Fact]
    public void Truncate_CutsLongCommandText()
    {
        var result = SlowDbCommandLoggingInterceptor.Truncate(new string('a', 1000));

        Assert.Equal(403, result.Length);
        Assert.EndsWith("...", result);
    }
}
