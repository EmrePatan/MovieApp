using MovieApp.Api.Diagnostics;

namespace MovieApp.UnitTests.Diagnostics;

public sealed class ForwardedForHeaderParserTests
{
    [Fact]
    public void ParseReturnsEmptyResultForMissingHeader()
    {
        var result = ForwardedForHeaderParser.Parse(null);

        Assert.Equal(0, result.HopCount);
        Assert.Empty(result.Chain);
    }

    [Fact]
    public void ParseSplitsCommaSeparatedChainAndTrimsWhitespace()
    {
        var result = ForwardedForHeaderParser.Parse(" 203.0.113.1 , 198.51.100.2 ");

        Assert.Equal(2, result.HopCount);
        Assert.Equal(["203.0.113.1", "198.51.100.2"], result.Chain);
    }

    [Fact]
    public void ParseNormalizesIpv4MappedToIpv6Hops()
    {
        var result = ForwardedForHeaderParser.Parse("::ffff:203.0.113.1");

        Assert.Equal(1, result.HopCount);
        Assert.Equal(["203.0.113.1"], result.Chain);
    }
}
