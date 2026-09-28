using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class SupportedContentLocalesAcceptLanguageTests
{
    [Fact]
    public void ResolveFromAcceptLanguage_SkipsUnsupportedHigherPriorityLocale()
    {
        var resolved = SupportedContentLocales.ResolveFromAcceptLanguage("ja-JP;q=1.0,tr-TR;q=0.9");

        Assert.Equal(SupportedContentLocales.TurkishTurkey, resolved);
    }
}
