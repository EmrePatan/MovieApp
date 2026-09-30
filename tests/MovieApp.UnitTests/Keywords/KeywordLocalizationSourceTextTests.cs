using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordLocalizationSourceTextTests
{
    [Fact]
    public void ResolveSourceText_PrefersCanonicalName()
    {
        var keyword = new Keyword
        {
            Name = "legacy",
            CanonicalName = "time travel",
        };

        Assert.Equal("time travel", KeywordLocalizationSourceText.ResolveSourceText(keyword));
    }

    [Fact]
    public void ComputeSourceTextHash_IsDeterministic()
    {
        var first = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var second = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }
}
