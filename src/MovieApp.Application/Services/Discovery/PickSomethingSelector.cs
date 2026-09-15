using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Services.Discovery;

internal static class PickSomethingSelector
{
    internal const int CandidatePoolSize = 30;
    internal const int SelectionBandSize = 10;

    internal static RecommendationItem? SelectFromBand(IReadOnlyList<RecommendationItem> rankedItems)
    {
        if (rankedItems.Count == 0)
        {
            return null;
        }

        var band = rankedItems.Take(SelectionBandSize).ToList();
        if (band.Count == 1)
        {
            return band[0];
        }

        var weights = band
            .Select(item => (double)Math.Max((double)item.Score, 0.01d))
            .ToList();
        var totalWeight = weights.Sum();
        var roll = Random.Shared.NextDouble() * totalWeight;
        var cumulative = 0d;

        for (var index = 0; index < band.Count; index++)
        {
            cumulative += weights[index];
            if (roll <= cumulative)
            {
                return band[index];
            }
        }

        return band[^1];
    }
}
