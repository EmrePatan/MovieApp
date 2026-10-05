namespace MovieApp.Application.Recommendations;

public static class RecommendationStratifiedGenreSelection
{
    public const int MaxStratifiedGenres = 8;

    public static List<Guid> PrepareGenreOrder(IReadOnlyList<Guid> preferredGenreIds) =>
        preferredGenreIds
            .Where(genreId => genreId != Guid.Empty)
            .Distinct()
            .Take(MaxStratifiedGenres)
            .ToList();

    public static int ComputePerGenreLimit(int maxCandidates, int genreCount, int candidateMinPerGenre) =>
        Math.Min(
            maxCandidates,
            Math.Max(candidateMinPerGenre, maxCandidates / genreCount));

    public static List<IReadOnlyList<Guid>> BuildBuckets(
        IReadOnlyList<Guid> genreOrder,
        IEnumerable<StratifiedGenreVoteRow> rows,
        int perGenreLimit)
    {
        var rowsByGenre = rows
            .GroupBy(row => row.GenreId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group
                    .OrderByDescending(row => row.VoteCount)
                    .ThenByDescending(row => row.VoteAverage)
                    .ThenBy(row => row.ContentId)
                    .Take(perGenreLimit)
                    .Select(row => row.ContentId)
                    .ToList());

        var buckets = new List<IReadOnlyList<Guid>>(genreOrder.Count);
        foreach (var genreId in genreOrder)
        {
            buckets.Add(rowsByGenre.GetValueOrDefault(genreId) ?? []);
        }

        return buckets;
    }
}
