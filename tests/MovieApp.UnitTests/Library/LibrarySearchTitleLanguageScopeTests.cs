using MovieApp.Application.Services.Library;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Library;

public sealed class LibrarySearchTitleLanguageScopeTests
{
    [Fact]
    public void FromContentLocale_IncludesEnglishForNonEnglishLocales()
    {
        var scope = LibrarySearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("tr", scope.PrimaryLanguageCode);
        Assert.Equal("TR", scope.PrimaryRegionCode);
        Assert.True(scope.IncludeEnglishLanguage);
    }

    [Fact]
    public void FromContentLocale_OmitsRedundantEnglishForEnglishLocale()
    {
        var scope = LibrarySearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("en", scope.PrimaryLanguageCode);
        Assert.Equal("US", scope.PrimaryRegionCode);
        Assert.False(scope.IncludeEnglishLanguage);
    }
}
