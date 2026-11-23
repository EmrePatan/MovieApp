namespace MovieApp.Application.Services.Insights;

public sealed record KeywordAffinityResult(int MatchPercent, IReadOnlyList<Guid> SharedKeywordIds);

public static class KeywordAffinityCalculator
{
    public const int MinKeywordsPerSide = 3;
    public const int MaxSharedKeywords = 6;

    /// <summary>
    /// Cosine similarity of the two keyword-count vectors (0-100); shared keywords are ranked by
    /// the smaller of the two counts. Null when either side has too little keyword data to judge.
    /// </summary>
    public static KeywordAffinityResult? Compute(
        IReadOnlyDictionary<Guid, int> mine,
        IReadOnlyDictionary<Guid, int> theirs)
    {
        if (mine.Count < MinKeywordsPerSide || theirs.Count < MinKeywordsPerSide)
        {
            return null;
        }

        double dot = 0;
        foreach (var (id, count) in mine)
        {
            if (theirs.TryGetValue(id, out var other))
            {
                dot += (double)count * other;
            }
        }

        var norm = Math.Sqrt(mine.Values.Sum(v => (double)v * v))
            * Math.Sqrt(theirs.Values.Sum(v => (double)v * v));
        var percent = norm <= 0
            ? 0
            : (int)Math.Clamp(Math.Round(dot / norm * 100, MidpointRounding.AwayFromZero), 0, 100);

        var shared = mine
            .Where(pair => theirs.ContainsKey(pair.Key))
            .OrderByDescending(pair => Math.Min(pair.Value, theirs[pair.Key]))
            .ThenBy(pair => pair.Key)
            .Take(MaxSharedKeywords)
            .Select(pair => pair.Key)
            .ToList();

        return new KeywordAffinityResult(percent, shared);
    }
}
