using MovieApp.Application.Abstractions.Keywords;

namespace MovieApp.Application.Recommendations;

public static class KeywordCatalogStatisticsMath
{
    public static IReadOnlyDictionary<Guid, double> BuildIdfWeights(
        IReadOnlyDictionary<Guid, int> documentFrequency,
        int catalogItemCount)
    {
        var n = Math.Max(1, catalogItemCount);
        var weights = new Dictionary<Guid, double>(documentFrequency.Count);
        foreach (var (keywordId, documentFrequencyCount) in documentFrequency)
        {
            weights[keywordId] = Math.Log((n + 1d) / (Math.Max(0, documentFrequencyCount) + 1d)) + 1d;
        }

        return weights;
    }

    public static bool AppliesGenericDampening(
        int documentFrequency,
        int catalogDocumentCount,
        double genericDocumentFrequencyRatio) =>
        documentFrequency / (double)Math.Max(1, catalogDocumentCount) >= genericDocumentFrequencyRatio;

    public static double GetPreferenceWeightMultiplier(
        IKeywordCatalogStatisticsSnapshot? snapshot,
        Guid keywordId,
        double genericDampeningFactor)
    {
        if (snapshot is null || !snapshot.IsAvailable ||
            !snapshot.TryGetStatistics(keywordId, out var entry))
        {
            return 1d;
        }

        var weight = entry.InverseDocumentFrequency;
        if (entry.AppliesGenericDampening)
        {
            weight *= genericDampeningFactor;
        }

        return weight;
    }
}
