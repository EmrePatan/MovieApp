using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class ContentLocaleResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("en-GB;q=0.9")]
    [InlineData("de")]
    [InlineData("fr-FR")]
    public void ResolveFromAcceptLanguage_ReturnsEnglishUnitedStates_ForUnsupportedOrMissing(string? header)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(header);

        Assert.Equal(ContentLocaleResolver.EnglishUnitedStates, locale);
        Assert.False(ContentLocaleResolver.RequiresLocalization(locale));
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("tr-TR")]
    [InlineData("tr-TR,en-US;q=0.8")]
    public void ResolveFromAcceptLanguage_ReturnsTurkishTurkey_ForTurkishHeader(string header)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(header);

        Assert.Equal(ContentLocaleResolver.TurkishTurkey, locale);
        Assert.True(ContentLocaleResolver.RequiresLocalization(locale));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("es-ES")]
    [InlineData("es-ES,en-US;q=0.8")]
    [InlineData("en-US;q=0.5, es-ES;q=0.9")]
    public void ResolveFromAcceptLanguage_ReturnsSpanishSpain_ForSpanishHeader(string header)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(header);

        Assert.Equal(ContentLocaleResolver.SpanishSpain, locale);
        Assert.True(ContentLocaleResolver.RequiresLocalization(locale));
    }

    [Fact]
    public void ResolveFromAcceptLanguage_PrefersHigherQualitySpanishOverEnglish()
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage("en-US;q=0.4, es-ES;q=0.9");

        Assert.Equal(ContentLocaleResolver.SpanishSpain, locale);
    }

    [Theory]
    [InlineData("en", ContentLocaleResolver.EnglishUnitedStates)]
    [InlineData("en-US", ContentLocaleResolver.EnglishUnitedStates)]
    [InlineData("tr", ContentLocaleResolver.TurkishTurkey)]
    [InlineData("tr-TR", ContentLocaleResolver.TurkishTurkey)]
    [InlineData("es", ContentLocaleResolver.SpanishSpain)]
    [InlineData("es-ES", ContentLocaleResolver.SpanishSpain)]
    public void Normalize_ReturnsSupportedLocale(string input, string expected)
    {
        Assert.Equal(expected, ContentLocaleResolver.Normalize(input));
    }
}
