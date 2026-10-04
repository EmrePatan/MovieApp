using MovieApp.Application.Common;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class LocalizedDisplayTitleSelectorTests
{
    [Fact]
    public void EnglishLocale_ReturnsCanonicalTitleWithoutSubtitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            "Alev Alev",
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Flames of Fate", titles.Title);
        Assert.Equal("Alev Alev", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_PansLabyrinth_UsesEnglishPrimaryAndTurkishSubtitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Pan's Labyrinth",
            "El laberinto del fauno",
            "es",
            "Pan'ın Labirenti",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Pan's Labyrinth", titles.Title);
        Assert.Equal("Pan'ın Labirenti", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_TurkishProduction_UsesTurkishPrimaryAndEnglishSubtitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            "Alev Alev",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Alev Alev", titles.Title);
        Assert.Equal("Flames of Fate", titles.OriginalTitle);
    }

    [Fact]
    public void GermanLocale_GermanProduction_UsesGermanPrimaryAndEnglishSubtitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "The Lives of Others",
            "Das Leben der Anderen",
            "de",
            "Das Leben der Anderen",
            ContentLocaleResolver.GermanGermany);

        Assert.Equal("Das Leben der Anderen", titles.Title);
        Assert.Equal("The Lives of Others", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_TurkishProduction_IgnoresExportLocalizedTitle_WhenItRepeatsCanonical()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            "Flames of Fate",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Alev Alev", titles.Title);
        Assert.Equal("Flames of Fate", titles.OriginalTitle);
    }

    [Fact]
    public void GermanLocale_GermanProduction_IgnoresExportLocalizedTitle_WhenItRepeatsCanonical()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "The Lives of Others",
            "Das Leben der Anderen",
            "de",
            "The Lives of Others",
            ContentLocaleResolver.GermanGermany);

        Assert.Equal("Das Leben der Anderen", titles.Title);
        Assert.Equal("The Lives of Others", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_TurkishOriginCountry_UsesLocalizedPrimary_WhenOriginalLanguageIsEnglish()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Not a Stranger",
            "Not a Stranger",
            "en",
            "Seni Tanıyorum",
            ContentLocaleResolver.TurkishTurkey,
            primaryOriginCountryCode: "TR");

        Assert.Equal("Seni Tanıyorum", titles.Title);
        Assert.Equal("Not a Stranger", titles.OriginalTitle);
    }

    [Fact]
    public void GermanLocale_GermanOriginCountry_UsesLocalizedPrimary_WhenOriginalLanguageIsEnglish()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "The Bridge",
            "The Bridge",
            "en",
            "Die Brücke",
            ContentLocaleResolver.GermanGermany,
            primaryOriginCountryCode: "DE");

        Assert.Equal("Die Brücke", titles.Title);
        Assert.Equal("The Bridge", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_KoreanProduction_UsesEnglishPrimaryAndTurkishSubtitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Parasite",
            "Gisaengchung",
            "ko",
            "Parazit",
            ContentLocaleResolver.TurkishTurkey,
            primaryOriginCountryCode: "KR");

        Assert.Equal("Parasite", titles.Title);
        Assert.Equal("Parazit", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_ForeignEnglishProduction_UsesCanonicalPrimary()
    {
        var title = LocalizedDisplayTitleSelector.ChoosePrimary(
            "Interstellar",
            "Interstellar",
            "en",
            "Yıldızlararası",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Interstellar", title);
    }

    [Fact]
    public void TurkishLocale_ForeignWithoutLocalizedTitle_HasNoDuplicateSubtitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Unknown Film",
            "Original Name",
            "fr",
            localizedTitle: null,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Unknown Film", titles.Title);
        Assert.Equal("Original Name", titles.OriginalTitle);
    }

    [Fact]
    public void MissingOriginalLanguage_UsesCanonicalPrimaryForImportedTitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Pan's Labyrinth",
            "El laberinto del fauno",
            originalLanguage: null,
            "Pan'ın Labirenti",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Pan's Labyrinth", titles.Title);
        Assert.Equal("Pan'ın Labirenti", titles.OriginalTitle);
    }

    [Fact]
    public void EquivalentTitles_SuppressSubtitle()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Pan's Labyrinth",
            "El laberinto del fauno",
            "es",
            "Pan's Labyrinth",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Pan's Labyrinth", titles.Title);
        Assert.Equal("El laberinto del fauno", titles.OriginalTitle);
    }

    [Fact]
    public void DisplayTitleEquivalence_TreatsApostropheVariantsAsEqual()
    {
        Assert.True(DisplayTitleEquivalence.AreEquivalent("Pan's Labyrinth", "Pans Labyrinth"));
    }

    [Fact]
    public void IsTurkishProduction_UsesHeuristicsForPosterPaths()
    {
        var isTurkish = ContentProductionHeuristics.IsTurkishProduction(
            originalLanguage: null,
            primaryOriginCountryCode: null,
            originalTitle: "Hababam Sınıfı");

        Assert.True(isTurkish);
    }
}
