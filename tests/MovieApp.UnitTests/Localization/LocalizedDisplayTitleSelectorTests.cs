using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class LocalizedDisplayTitleSelectorTests
{
    [Fact]
    public void EnglishLocale_ReturnsCanonicalTitle()
    {
        var title = LocalizedDisplayTitleSelector.ChoosePrimary(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            "Alev Alev",
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Flames of Fate", title);
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
    public void TurkishLocale_ForeignProduction_ShowsTurkishSubtitleWhenLocalizedTitleExists()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Interstellar",
            "Interstellar",
            "en",
            "Yıldızlararası",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Interstellar", titles.Title);
        Assert.Equal("Yıldızlararası", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_ForeignProduction_IgnoresLocalizedTurkishTitle()
    {
        var title = LocalizedDisplayTitleSelector.ChoosePrimary(
            "Resident Evil",
            "Resident Evil",
            "en",
            "Ölümcül Deney",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Resident Evil", title);
    }

    [Fact]
    public void TurkishLocale_TurkishProduction_FallsBackToOriginalWhenLocalizedMissing()
    {
        var title = LocalizedDisplayTitleSelector.ChoosePrimary(
            "Flames of Fate",
            "Alev Alev",
            "tr",
            localizedTitle: null,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Alev Alev", title);
    }

    [Fact]
    public void TurkishLocale_ForeignProduction_FallsBackToCanonicalWhenOriginalMissing()
    {
        var title = LocalizedDisplayTitleSelector.ChoosePrimary(
            "Family Guy",
            originalTitle: null,
            originalLanguage: "en",
            localizedTitle: "Family Guy TR",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Family Guy", title);
    }

    [Fact]
    public void TurkishLocale_EnglishOriginalLanguage_PrefersCanonicalTitleWhenOriginalTitleIsLocalized()
    {
        var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
            "Thor: Love and Thunder",
            "Thor: Aşk ve Gök Gürültüsü",
            "en",
            "Thor: Aşk ve Gök Gürültüsü",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Thor: Love and Thunder", titles.Title);
        Assert.Equal("Thor: Aşk ve Gök Gürültüsü", titles.OriginalTitle);
    }

    [Fact]
    public void TurkishLocale_TurkishOriginCountry_UsesTurkishPrimaryAndEnglishSubtitle()
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
    public void IsTurkishProduction_TreatsTurkishOriginalTitleAsDomesticWhenLanguageMissing()
    {
        var isTurkish = LocalizedDisplayTitleSelector.IsTurkishProduction(
            originalLanguage: null,
            primaryOriginCountryCode: null,
            originalTitle: "Hababam Sınıfı");

        Assert.True(isTurkish);
    }
}
