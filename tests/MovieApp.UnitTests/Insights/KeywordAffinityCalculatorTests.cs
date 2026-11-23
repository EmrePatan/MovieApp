using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class KeywordAffinityCalculatorTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid E = Guid.Parse("00000000-0000-0000-0000-00000000000e");
    private static readonly Guid F = Guid.Parse("00000000-0000-0000-0000-00000000000f");

    [Fact]
    public void IdenticalVectorsScoreHundredAndDisjointScoreZero()
    {
        var mine = new Dictionary<Guid, int> { [A] = 5, [B] = 3, [C] = 1 };
        var other = new Dictionary<Guid, int> { [D] = 4, [E] = 2, [F] = 1 };

        Assert.Equal(100, KeywordAffinityCalculator.Compute(mine, mine)!.MatchPercent);
        var disjoint = KeywordAffinityCalculator.Compute(mine, other)!;
        Assert.Equal(0, disjoint.MatchPercent);
        Assert.Empty(disjoint.SharedKeywordIds);
    }

    [Fact]
    public void SharedKeywordsAreRankedBySmallerCount()
    {
        var mine = new Dictionary<Guid, int> { [A] = 9, [B] = 4, [C] = 1 };
        var theirs = new Dictionary<Guid, int> { [A] = 2, [B] = 4, [D] = 1 };

        var result = KeywordAffinityCalculator.Compute(mine, theirs)!;

        Assert.Equal([B, A], result.SharedKeywordIds);
    }

    [Fact]
    public void TooLittleKeywordDataYieldsNull()
    {
        var thin = new Dictionary<Guid, int> { [A] = 1, [B] = 1 };
        var enough = new Dictionary<Guid, int> { [A] = 1, [B] = 1, [C] = 1 };

        Assert.Null(KeywordAffinityCalculator.Compute(thin, enough));
    }
}
