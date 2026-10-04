using MovieApp.Application.Common;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class SearchAliasLocaleRankingTests
{
    [Fact]
    public void MatchesRequestedLocale_UsesPrimaryLanguageCode()
    {
        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.TurkishTurkey);

        Assert.True(SearchAliasLocaleRanking.MatchesRequestedLocale("tr", "TR", scope));
        Assert.False(SearchAliasLocaleRanking.MatchesRequestedLocale("de", "DE", scope));
    }

    [Fact]
    public void MatchesEnglish_RespectsScopeIncludeEnglishFlag()
    {
        var turkishScope = CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.TurkishTurkey);
        var englishScope = CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(SearchAliasLocaleRanking.MatchesEnglish("en", turkishScope));
        Assert.False(SearchAliasLocaleRanking.MatchesEnglish("en", englishScope));
    }

    [Fact]
    public void PromotionConstants_PlaceLocaleAheadOfEnglishAheadOfOtherAliases()
    {
        var localePromoted = SearchBestMatchTier.DirectExactAlias + SearchAliasLocaleRanking.RequestedLocaleDirectPromotion;
        var englishPromoted = SearchBestMatchTier.DirectExactAlias + SearchAliasLocaleRanking.EnglishDirectPromotion;
        var otherAlias = SearchBestMatchTier.DirectExactAlias;

        Assert.Equal(SearchBestMatchTier.DirectExactCanonical, localePromoted);
        Assert.Equal(SearchBestMatchTier.DirectExactOriginal, englishPromoted);
        Assert.True(localePromoted < englishPromoted);
        Assert.True(englishPromoted < otherAlias);
    }
}
