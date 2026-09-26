using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class LocalizedDisplayTitleSelectorTests
{
    [Fact]
    public void EnglishLocale_ReturnsCanonicalTitle()
    {
        var title = LocalizedDisplayTitleSelector.Choose(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            "Alev Alev",
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Flames of Fate", title);
    }

    [Fact]
    public void TurkishLocale_PrefersLocalizedTitle_WhenDifferentFromCanonical()
    {
        var title = LocalizedDisplayTitleSelector.Choose(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            "Alev Alev",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Alev Alev", title);
    }

    [Fact]
    public void TurkishLocale_PrefersOriginalTitle_WhenLocalizedMissingAndOriginalDiffers()
    {
        var title = LocalizedDisplayTitleSelector.Choose(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            localizedTitle: null,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Alev Alev", title);
    }

    [Fact]
    public void TurkishLocale_PrefersOriginalTitle_WhenLocalizedEqualsCanonical()
    {
        var title = LocalizedDisplayTitleSelector.Choose(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            "Flames of Fate",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Alev Alev", title);
    }

    [Fact]
    public void TurkishLocale_FallsBackToCanonical_WhenNoLocalizedOrOriginalSignal()
    {
        var title = LocalizedDisplayTitleSelector.Choose(
            "Interstellar",
            originalTitle: null,
            originalLanguage: null,
            localizedTitle: null,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Interstellar", title);
    }

    [Fact]
    public void TurkishLocale_RetainsCanonicalEnglishOverviewFallbackSemantics_UnchangedForTitle()
    {
        var title = LocalizedDisplayTitleSelector.Choose(
            "Family Guy",
            "Family Guy",
            "en",
            localizedTitle: null,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Family Guy", title);
    }
}
