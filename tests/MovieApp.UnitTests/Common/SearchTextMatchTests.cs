using MovieApp.Application.Common;

namespace MovieApp.UnitTests.Common;

public sealed class SearchTextMatchTests
{
    [Fact]
    public void FromQueryPreservesTurkishDotlessLettersWithoutInvariantLowercasing()
    {
        var match = SearchTextMatch.FromQuery("  ISLIK  ");
        Assert.Equal("ISLIK", match.Primary);
        Assert.Null(match.TurkishAlternate);
    }

    [Fact]
    public void FromQueryBuildsTurkishAlternateForMixedTurkishQuery()
    {
        var match = SearchTextMatch.FromQuery("Islık");
        Assert.Equal("Islık", match.Primary);
        Assert.Equal("ıslık", match.TurkishAlternate);
    }
}
