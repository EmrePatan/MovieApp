using MovieApp.Application.Services.Keywords;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordCanonicalNormalizationTests
{
    [Theory]
    [InlineData(" Time-Travel ", "time travel")]
    [InlineData("SCI_FI", "sci fi")]
    [InlineData("a   b    c", "a b c")]
    public void NormalizeKeywordName_CollapsesSeparatorsAndWhitespace(string input, string expected)
    {
        Assert.Equal(expected, KeywordCanonicalNormalization.NormalizeKeywordName(input));
    }

    [Fact]
    public void NormalizeKeywordName_AppliesUnicodeNfkc()
    {
        var input = "\uFB01ght"; // ﬁ ligature
        var expected = KeywordCanonicalNormalization.NormalizeKeywordName("fight");
        Assert.Equal(expected, KeywordCanonicalNormalization.NormalizeKeywordName(input));
    }

    [Fact]
    public void NormalizeKeywordName_ConvertsHyphenToSpaceWithoutStrippingOtherPunctuation()
    {
        Assert.Equal("sci fi", KeywordCanonicalNormalization.NormalizeKeywordName("sci-fi"));
        Assert.Equal("u.s.a.", KeywordCanonicalNormalization.NormalizeKeywordName("U.S.A."));
    }
}
