using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class ContentLocaleResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ja")]
    [InlineData("ja-JP;q=0.9")]
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

    [Theory]
    [InlineData("de")]
    [InlineData("de-DE")]
    [InlineData("de-DE,en-US;q=0.8")]
    public void ResolveFromAcceptLanguage_ReturnsGermanGermany_ForGermanHeader(string header)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(header);

        Assert.Equal(ContentLocaleResolver.GermanGermany, locale);
        Assert.True(ContentLocaleResolver.RequiresLocalization(locale));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("fr-FR")]
    [InlineData("fr-FR,en-US;q=0.8")]
    public void ResolveFromAcceptLanguage_ReturnsFrenchFrance_ForFrenchHeader(string header)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(header);

        Assert.Equal(ContentLocaleResolver.FrenchFrance, locale);
        Assert.True(ContentLocaleResolver.RequiresLocalization(locale));
    }

    [Theory]
    [InlineData("it")]
    [InlineData("it-IT")]
    [InlineData("it-IT,en-US;q=0.8")]
    public void ResolveFromAcceptLanguage_ReturnsItalianItaly_ForItalianHeader(string header)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(header);

        Assert.Equal(ContentLocaleResolver.ItalianItaly, locale);
        Assert.True(ContentLocaleResolver.RequiresLocalization(locale));
    }

    [Theory]
    [InlineData("pt")]
    [InlineData("pt-BR")]
    [InlineData("pt-BR,en-US;q=0.8")]
    [InlineData("pt-PT")]
    public void ResolveFromAcceptLanguage_ReturnsPortugueseBrazil_ForPortugueseHeader(string header)
    {
        var locale = ContentLocaleResolver.ResolveFromAcceptLanguage(header);

        Assert.Equal(ContentLocaleResolver.PortugueseBrazil, locale);
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
    [InlineData("de", ContentLocaleResolver.GermanGermany)]
    [InlineData("de-DE", ContentLocaleResolver.GermanGermany)]
    [InlineData("fr", ContentLocaleResolver.FrenchFrance)]
    [InlineData("fr-FR", ContentLocaleResolver.FrenchFrance)]
    [InlineData("it", ContentLocaleResolver.ItalianItaly)]
    [InlineData("it-IT", ContentLocaleResolver.ItalianItaly)]
    [InlineData("pt", ContentLocaleResolver.PortugueseBrazil)]
    [InlineData("pt-BR", ContentLocaleResolver.PortugueseBrazil)]
    public void Normalize_ReturnsSupportedLocale(string input, string expected)
    {
        Assert.Equal(expected, ContentLocaleResolver.Normalize(input));
    }
}
