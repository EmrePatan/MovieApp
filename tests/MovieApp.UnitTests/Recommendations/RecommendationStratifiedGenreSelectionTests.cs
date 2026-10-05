using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationStratifiedGenreSelectionTests
{
    private static readonly Guid GenreA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid GenreB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid GenreC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void PrepareGenreOrder_DeduplicatesAndCapsAtEight()
    {
        var input = Enumerable.Range(0, 10)
            .Select(_ => Guid.NewGuid())
            .Prepend(GenreA)
            .Prepend(GenreA)
            .ToList();

        var order = RecommendationStratifiedGenreSelection.PrepareGenreOrder(input);

        Assert.Equal(8, order.Count);
        Assert.Equal(GenreA, order[0]);
        Assert.Equal(input[2], order[1]);
    }

    [Fact]
    public void ComputePerGenreLimit_UsesMinimumPerGenreFloor()
    {
        var limit = RecommendationStratifiedGenreSelection.ComputePerGenreLimit(
            maxCandidates: 500,
            genreCount: 8,
            candidateMinPerGenre: 8);

        Assert.Equal(62, limit);
    }

    [Fact]
    public void BuildBuckets_OrdersByVoteLeadersPerGenre()
    {
        var movieHigh = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var movieLow = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var rows = new[]
        {
            new StratifiedGenreVoteRow(GenreA, movieLow, 100, 7.0m),
            new StratifiedGenreVoteRow(GenreA, movieHigh, 500, 8.0m)
        };

        var buckets = RecommendationStratifiedGenreSelection.BuildBuckets([GenreA], rows, perGenreLimit: 1);

        Assert.Equal(movieHigh, buckets[0][0]);
    }

    [Fact]
    public void BuildBuckets_PreservesGenrePriorityOrder()
    {
        var buckets = RecommendationStratifiedGenreSelection.BuildBuckets(
            [GenreB, GenreA],
            [],
            perGenreLimit: 3);

        Assert.Equal(2, buckets.Count);
        Assert.Empty(buckets[0]);
        Assert.Empty(buckets[1]);
    }

    [Fact]
    public void RoundRobin_MatchesStratifiedBucketSemanticsForOverlappingCandidates()
    {
        var shared = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var onlyA = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var onlyB = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var rows = new[]
        {
            new StratifiedGenreVoteRow(GenreA, shared, 1_000, 8m),
            new StratifiedGenreVoteRow(GenreA, onlyA, 900, 8m),
            new StratifiedGenreVoteRow(GenreB, shared, 1_000, 8m),
            new StratifiedGenreVoteRow(GenreB, onlyB, 900, 8m)
        };

        var buckets = RecommendationStratifiedGenreSelection.BuildBuckets([GenreA, GenreB], rows, perGenreLimit: 2);
        var selected = RecommendationCandidateBudget.RoundRobin(buckets, maxCandidates: 3);

        Assert.Equal([shared, onlyA, onlyB], selected);
    }

    [Fact]
    public void RoundRobin_FillAppendsDistinctAfterSparseBuckets()
    {
        var buckets = RecommendationStratifiedGenreSelection.BuildBuckets(
            [GenreA, GenreB],
            [new StratifiedGenreVoteRow(GenreA, Guid.NewGuid(), 100, 7m)],
            perGenreLimit: 2);

        var partial = RecommendationCandidateBudget.RoundRobin(buckets, maxCandidates: 4);
        var fill = new[] { Guid.NewGuid(), partial[0] };
        var completed = RecommendationCandidateBudget.AppendDistinct(partial, fill, maxCandidates: 4);

        Assert.Equal(2, completed.Count);
        Assert.Equal(partial[0], completed[0]);
    }

    [Fact]
    public void BuildBuckets_MatchesSequentialPerGenreTakeOnSameRows()
    {
        var rows = CreateDeterministicFixtureRows();
        var genreOrder = new[] { GenreA, GenreB, GenreC };
        const int perGenre = 2;
        const int maxCandidates = 5;

        var batchedBuckets = RecommendationStratifiedGenreSelection.BuildBuckets(genreOrder, rows, perGenre);
        var sequentialBuckets = genreOrder
            .Select(genreId => (IReadOnlyList<Guid>)rows
                .Where(row => row.GenreId == genreId)
                .OrderByDescending(row => row.VoteCount)
                .ThenByDescending(row => row.VoteAverage)
                .ThenBy(row => row.ContentId)
                .Take(perGenre)
                .Select(row => row.ContentId)
                .ToList())
            .ToList();

        Assert.Equal(sequentialBuckets, batchedBuckets);
        Assert.Equal(
            RecommendationCandidateBudget.RoundRobin(sequentialBuckets, maxCandidates),
            RecommendationCandidateBudget.RoundRobin(batchedBuckets, maxCandidates));
    }

    private static StratifiedGenreVoteRow[] CreateDeterministicFixtureRows()
    {
        var ids = Enumerable.Range(0, 9)
            .Select(index => Guid.Parse($"00000000-0000-0000-0000-{index:D12}"))
            .ToArray();

        return
        [
            new(GenreA, ids[0], 1_000, 8.1m),
            new(GenreA, ids[1], 900, 8.0m),
            new(GenreA, ids[2], 800, 7.9m),
            new(GenreB, ids[1], 950, 8.2m),
            new(GenreB, ids[3], 700, 7.5m),
            new(GenreC, ids[4], 600, 7.0m)
        ];
    }
}
