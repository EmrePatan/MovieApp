using MovieApp.Application.Common;

namespace MovieApp.UnitTests.Search;

/// <summary>
/// Documents autocomplete text handling before provider HTTP and local PostgreSQL paths.
/// </summary>
public sealed class AutocompleteTurkishPipelineDiagnosticsTests
{
    [Theory]
    [InlineData("dönersen")]
    [InlineData("dönersen ı")]
    [InlineData("dönersen I")]
    [InlineData("dönersen is")]
    [InlineData("dönersen ıs")]
    [InlineData("dönersen ısl")]
    [InlineData("dönersen ıslı")]
    [InlineData("dönersen ıslık")]
    public void SearchTextMatchDocumentsProviderAndLocalNormalization(string userQuery)
    {
        var collapsed = QueryNormalizer.CollapseWhitespace(userQuery);
        var match = SearchTextMatch.FromQuery(userQuery);

        Assert.Equal(userQuery, collapsed);
        Assert.Equal(userQuery, match.Primary);
        Assert.Equal(SearchTurkishCaseFolder.TryCreateAlternate(userQuery), match.TurkishAlternate);
    }

    [Theory]
    [InlineData('I', 0x49)]
    [InlineData('i', 0x69)]
    [InlineData('İ', 0x130)]
    [InlineData('ı', 0x131)]
    public void TurkishLetterCodePoints(char letter, int expectedCodePoint)
    {
        Assert.Equal(expectedCodePoint, char.ConvertToUtf32(letter.ToString(), 0));
    }
}
