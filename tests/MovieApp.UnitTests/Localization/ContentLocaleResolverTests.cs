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
    public void ResolveFromAcceptLanguage_ReturnsEnglishUnitedStates_ForNonTurkishOrMissing(string? header)
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
}
