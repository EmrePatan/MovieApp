using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordDiscoverLocalizationSupportTests
{
    [Theory]
    [InlineData("time travel", "time travel")]
    [InlineData("Time Travel", "time travel")]
    [InlineData("zaman yolculuğu", "zaman yolculuğu")]
    public void NormalizeSearchName_UsesCanonicalNormalization(string input, string expected)
    {
        Assert.Equal(expected, KeywordDiscoverLocalizationSupport.NormalizeSearchName(input));
    }

    [Fact]
    public void NormalizeSearchName_TurkishDottedCapitalI_UsesInvariantLower()
    {
        var normalized = KeywordDiscoverLocalizationSupport.NormalizeSearchName("İ");
        Assert.Equal("İ", normalized);
    }

    [Fact]
    public void ResolveDisplayName_FollowsLocaleThenEnglishThenCanonical()
    {
        Assert.Equal(
            "Zaman yolculuğu",
            KeywordDiscoverLocalizationSupport.ResolveDisplayName(
                "Zaman yolculuğu",
                "Time travel",
                "time travel",
                "time travel"));

        Assert.Equal(
            "Time travel",
            KeywordDiscoverLocalizationSupport.ResolveDisplayName(
                null,
                "Time travel",
                "time travel",
                "time travel"));

        Assert.Equal(
            "time travel",
            KeywordDiscoverLocalizationSupport.ResolveDisplayName(
                null,
                null,
                "time travel",
                "legacy name"));
    }

    [Fact]
    public void ComputeRankScore_PrefersExactLocalizedDisplayMatch()
    {
        var exact = KeywordDiscoverLocalizationSupport.ComputeRankScore(
            "Zaman yolculuğu",
            KeywordDiscoverLocalizationSupport.NormalizeSearchName("Zaman yolculuğu"),
            "Zaman yolculuğu",
            "time travel",
            "time travel",
            "Zaman yolculuğu",
            "Time travel");

        var contains = KeywordDiscoverLocalizationSupport.ComputeRankScore(
            "yolculu",
            KeywordDiscoverLocalizationSupport.NormalizeSearchName("yolculu"),
            "Zaman yolculuğu",
            "time travel",
            "time travel",
            "Zaman yolculuğu",
            "Time travel");

        Assert.True(exact < contains);
    }

    [Fact]
    public void NormalizeLocale_UsesSupportedContentLocales()
    {
        Assert.Equal(
            SupportedContentLocales.TurkishTurkey,
            KeywordDiscoverLocalizationSupport.NormalizeLocale("tr"));
    }
}
