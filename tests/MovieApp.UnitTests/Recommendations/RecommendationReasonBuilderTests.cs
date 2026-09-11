using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationReasonBuilderTests
{
    [Fact]
    public void BuildSimilarReasonUsesSharedGenreWhenAvailable()
    {
        var genreId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var source = CreateProfile("Interstellar", genreId, "Science Fiction");
        var candidate = CreateCandidate(genreId, "Science Fiction");

        var reason = RecommendationReasonBuilder.BuildSimilarReason(source, candidate);

        Assert.Equal("Because you liked Science Fiction", reason);
    }

    [Fact]
    public void BuildColdStartReasonsUseDiscoveryTerminology()
    {
        Assert.Equal("Popular right now", RecommendationReasonBuilder.BuildColdStartPopularReason());
        Assert.Equal("Trending", RecommendationReasonBuilder.BuildColdStartTrendingReason());
        Assert.Equal("Top rated", RecommendationReasonBuilder.BuildColdStartTopRatedReason());
    }

    private static SimilaritySourceProfile CreateProfile(string title, Guid genreId, string genreName) =>
        new(
            Guid.NewGuid(),
            "movie",
            title,
            [genreId],
            new Dictionary<Guid, string> { [genreId] = genreName },
            [],
            8.5m,
            2014);

    private static SimilarityCandidateProfile CreateCandidate(Guid genreId, string genreName) =>
        new(
            Guid.NewGuid(),
            "movie",
            "Candidate",
            null,
            null,
            null,
            null,
            new DateOnly(2015, 1, 1),
            8.0m,
            100,
            2015,
            [genreId],
            new Dictionary<Guid, string> { [genreId] = genreName },
            []);
}
