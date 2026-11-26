using MovieApp.Application.Services.StreamingLeaving;

namespace MovieApp.UnitTests.StreamingLeaving;

public sealed class IsoCountryCodesTests
{
    [Fact]
    public void ListHoldsTheIsoCodesAndKosovo()
    {
        Assert.Equal(250, IsoCountryCodes.Count);
    }

    [Theory]
    [InlineData("TR")]
    [InlineData("us")]
    [InlineData("GB")]
    [InlineData("DE")]
    [InlineData("FR")]
    [InlineData("ES")]
    [InlineData("IT")]
    [InlineData("NL")]
    [InlineData("CA")]
    [InlineData("AU")]
    [InlineData("XK")]
    public void RealCountriesAreAccepted(string code) => Assert.True(IsoCountryCodes.Contains(code));

    [Theory]
    [InlineData("ZZ")]
    [InlineData("QQ")]
    [InlineData("UK")]
    [InlineData("EU")]
    public void MadeUpOrReservedCodesAreRejected(string code) => Assert.False(IsoCountryCodes.Contains(code));
}
