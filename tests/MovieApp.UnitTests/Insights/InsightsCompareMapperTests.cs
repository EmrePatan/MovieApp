using MovieApp.Api.Mapping;
using MovieApp.Contracts.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsCompareMapperTests
{
    private static readonly Guid Drama = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Comedy = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Horror = Guid.Parse("00000000-0000-0000-0000-000000000003");

    [Fact]
    public void MatchIsOverlapOfGenreShares()
    {
        var mine = CreateResponse([(Drama, "Drama", 60m), (Comedy, "Comedy", 40m)], 600);
        var theirs = CreateResponse([(Drama, "Drama", 30m), (Horror, "Horror", 70m)], 300);

        var result = InsightsCompareMapper.Build(mine, theirs);

        Assert.Equal(30, result.MatchPercent);
        Assert.Equal(Drama, Assert.Single(result.SharedGenres).GenreId);
        Assert.Equal(600, result.MyWatchMinutes);
        Assert.Equal(300, result.TheirWatchMinutes);
    }

    [Fact]
    public void DisjointTastesScoreZeroAndIdenticalScoreHundred()
    {
        var a = CreateResponse([(Drama, "Drama", 100m)], 0);
        var b = CreateResponse([(Horror, "Horror", 100m)], 0);

        Assert.Equal(0, InsightsCompareMapper.Build(a, b).MatchPercent);
        Assert.Equal(100, InsightsCompareMapper.Build(a, a).MatchPercent);
    }

    private static InsightsV3Response CreateResponse(
        IEnumerable<(Guid Id, string Name, decimal Share)> genres,
        int totalMinutes)
    {
        var list = genres.Select(g => new InsightsTasteGenreResponse(g.Id, g.Name, g.Share, g.Share)).ToList();
        return new InsightsV3Response(
            null!,
            new InsightsV3MovieDnaResponse("", [], [], list, null!),
            null!,
            new InsightsV3TasteSectionResponse(list, null),
            new InsightsV3TimeInStoriesResponse(totalMinutes, 0, 0, 0, 0m),
            null!,
            null!,
            null!,
            null!);
    }
}
