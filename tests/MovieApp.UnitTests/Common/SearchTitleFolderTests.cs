using System.Text;
using MovieApp.Application.Common;

namespace MovieApp.UnitTests.Common;

public sealed class SearchTitleFolderTests
{
    [Theory]
    [InlineData("Dönersen Islık Çal", "donersen islik cal")]
    [InlineData("Şahsiyet", "sahsiyet")]
    [InlineData("Çukur", "cukur")]
    [InlineData("Gönül", "gonul")]
    [InlineData("Élite", "elite")]
    public void FoldExamples(string input, string expected) =>
        Assert.Equal(expected, SearchTitleFolder.Fold(input));

    [Theory]
    [InlineData('ı', "i")]
    [InlineData('İ', "i")]
    [InlineData('ş', "s")]
    [InlineData('Ş', "s")]
    [InlineData('ğ', "g")]
    [InlineData('Ğ', "g")]
    [InlineData('ü', "u")]
    [InlineData('Ü', "u")]
    [InlineData('ö', "o")]
    [InlineData('Ö', "o")]
    [InlineData('ç', "c")]
    [InlineData('Ç', "c")]
    public void FoldTurkishCharacters(char input, string expected) =>
        Assert.Equal(expected, SearchTitleFolder.Fold(input.ToString()));

    [Fact]
    public void FoldGermanEszettToAsciiSs()
    {
        Assert.Equal("strasse", SearchTitleFolder.Fold("Straße"));
        Assert.Equal("strasse", SearchTitleFolder.Fold("STRAẞE"));
    }

    [Fact]
    public void FoldCollapsesWhitespaceAndTrims()
    {
        Assert.Equal("donersen islik", SearchTitleFolder.Fold("  dönersen   islık  "));
    }

    [Fact]
    public void FoldNormalizesNfcAndNfdToSameFoldedValue()
    {
        const string nfc = "Élite";
        var nfd = nfc.Normalize(NormalizationForm.FormD);

        Assert.Equal(SearchTitleFolder.Fold(nfc), SearchTitleFolder.Fold(nfd));
    }

    [Fact]
    public void FoldUsesLowercaseForLatinLetters()
    {
        Assert.Equal("whistle if you", SearchTitleFolder.Fold("WHISTLE IF YOU"));
    }
}
