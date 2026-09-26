using MovieApp.Application.Common;

namespace MovieApp.UnitTests.Common;

public sealed class SearchTurkishCaseFolderTests
{
    [Theory]
    [InlineData("ıslık", true)]
    [InlineData("Islık", true)]
    [InlineData("İstanbul", true)]
    [InlineData("INTERSTELLAR", false)]
    [InlineData("Islik", false)]
    public void MayContainTurkishDetectsTurkishSpecificLetters(string query, bool expected)
    {
        Assert.Equal(expected, SearchTurkishCaseFolder.MayContainTurkish(query));
    }

    [Fact]
    public void TryCreateAlternateUsesTurkishRulesWhenTurkishLettersPresent()
    {
        var alternate = SearchTurkishCaseFolder.TryCreateAlternate("Islık");
        Assert.Equal("ıslık", alternate);
    }

    [Fact]
    public void TryCreateAlternateReturnsNullForAsciiOnlyQueries()
    {
        Assert.Null(SearchTurkishCaseFolder.TryCreateAlternate("INTERSTELLAR"));
    }
}
