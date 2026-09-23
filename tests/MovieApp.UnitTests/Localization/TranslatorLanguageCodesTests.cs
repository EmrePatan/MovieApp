using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class TranslatorLanguageCodesTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("tr-TR", "tr")]
    [InlineData("pt-BR", "pt")]
    public void FromContentLocaleMapsSupportedLocales(string contentLocale, string expected)
    {
        Assert.Equal(expected, TranslatorLanguageCodes.FromContentLocale(contentLocale));
    }

    [Theory]
    [InlineData("en", "en-US", true)]
    [InlineData("en-US", "en-US", true)]
    [InlineData("es", "en-US", false)]
    [InlineData("pt", "pt-BR", true)]
    public void SourceMatchesTargetComparesLanguagePrefixes(
        string detected,
        string targetContentLocale,
        bool expected) =>
        Assert.Equal(expected, TranslatorLanguageCodes.SourceMatchesTarget(detected, targetContentLocale));
}
