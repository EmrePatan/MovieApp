using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class SimilarityEngineTests
{
    private static readonly RecommendationOptions DefaultOptions = new();

    [Fact]
    public void CalculateScoreUsesGenreOverlap()
    {
        var source = CreateSource([Guid.Parse("11111111-1111-1111-1111-111111111111")]);
        var similar = CreateCandidate(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            [Guid.Parse("11111111-1111-1111-1111-111111111111")],
            8.5m,
            2014);
        var different = CreateCandidate(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            [Guid.Parse("99999999-9999-9999-9999-999999999999")],
            8.5m,
            2014);

        var similarScore = SimilarityEngine.CalculateScore(source, similar, DefaultOptions);
        var differentScore = SimilarityEngine.CalculateScore(source, different, DefaultOptions);

        Assert.True(similarScore > differentScore);
    }

    [Fact]
    public void RankSimilarCandidatesExcludesSourceAndOrdersDeterministically()
    {
        var source = CreateSource([Guid.Parse("11111111-1111-1111-1111-111111111111")], voteAverage: 8.0m);
        var candidates = new List<SimilarityCandidateProfile>
        {
            CreateCandidate(source.Id, [Guid.Parse("11111111-1111-1111-1111-111111111111")], 8.0m, 2014, voteCount: 100),
            CreateCandidate(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), [Guid.Parse("11111111-1111-1111-1111-111111111111")], 8.0m, 2014, voteCount: 200),
            CreateCandidate(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), [Guid.Parse("11111111-1111-1111-1111-111111111111")], 7.0m, 2014, voteCount: 300)
        };

        var ranked = SimilarityEngine.RankSimilarCandidates(source, candidates, DefaultOptions);

        Assert.Equal(2, ranked.Count);
        Assert.DoesNotContain(ranked, item => item.Candidate.Id == source.Id);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), ranked[0].Candidate.Id);
    }

    [Fact]
    public void CalculateScoreReflectsRatingAndYearProximity()
    {
        var source = CreateSource([Guid.Parse("11111111-1111-1111-1111-111111111111")], voteAverage: 8.0m);
        var closeMatch = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [Guid.Parse("11111111-1111-1111-1111-111111111111")],
            8.5m,
            2015);
        var farMatch = CreateCandidate(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [Guid.Parse("11111111-1111-1111-1111-111111111111")],
            2.0m,
            1990);

        var closeScore = SimilarityEngine.CalculateScore(source, closeMatch, DefaultOptions);
        var farScore = SimilarityEngine.CalculateScore(source, farMatch, DefaultOptions);

        Assert.True(closeScore > farScore);
    }

    private static SimilaritySourceProfile CreateSource(
        IReadOnlyList<Guid> genreIds,
        decimal voteAverage = 8.0m) =>
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "movie",
            "Source",
            genreIds,
            genreIds.ToDictionary(id => id, _ => "Science Fiction"),
            [],
            voteAverage,
            2014);

    private static SimilarityCandidateProfile CreateCandidate(
        Guid id,
        IReadOnlyList<Guid> genreIds,
        decimal voteAverage,
        int year,
        int voteCount = 100) =>
        new(
            id,
            "movie",
            $"Candidate-{id}",
            null,
            null,
            null,
            null,
            new DateOnly(year, 1, 1),
            voteAverage,
            voteCount,
            year,
            genreIds,
            genreIds.ToDictionary(genreId => genreId, _ => "Science Fiction"),
            []);
}
