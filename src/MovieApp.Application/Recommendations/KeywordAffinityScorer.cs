using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Recommendations;

public static class KeywordAffinityScorer
{
    public static IReadOnlyDictionary<Guid, decimal> BuildKeywordPreferences(
        IReadOnlyList<UserBehaviorSignal> signals,
        RecommendationOptions options,
        DateTime utcNow) =>
        BuildKeywordPreferences(signals, options, utcNow, catalogStatistics: null, statisticsOptions: null);

    public static IReadOnlyDictionary<Guid, decimal> BuildKeywordPreferences(
        IReadOnlyList<UserBehaviorSignal> signals,
        RecommendationOptions options,
        DateTime utcNow,
        IKeywordCatalogStatisticsSnapshot? catalogStatistics,
        KeywordCatalogStatisticsOptions? statisticsOptions)
    {
        var useFrequencyAware = statisticsOptions?.Enabled == true &&
                                catalogStatistics is { IsAvailable: true };

        var preferences = new Dictionary<Guid, decimal>();
        var genericDampeningFactor = statisticsOptions?.GenericDampeningFactor ?? 0.5d;

        foreach (var signal in signals)
        {
            var contribution = RecommendationSignalScoring.GetSignalContribution(signal, options, utcNow);
            if (contribution == 0m)
            {
                continue;
            }

            var keywordIds = signal.KeywordIds.Distinct().ToList();
            if (keywordIds.Count == 0)
            {
                continue;
            }

            var perKeywordContribution = contribution / (decimal)Math.Sqrt(keywordIds.Count);

            foreach (var keywordId in keywordIds)
            {
                var weightedContribution = perKeywordContribution;
                if (useFrequencyAware)
                {
                    var multiplier = KeywordCatalogStatisticsMath.GetPreferenceWeightMultiplier(
                        catalogStatistics,
                        keywordId,
                        genericDampeningFactor);
                    weightedContribution *= (decimal)multiplier;
                }

                preferences[keywordId] = preferences.GetValueOrDefault(keywordId) + weightedContribution;
            }
        }

        return NormalizePreferences(preferences);
    }

    internal static IReadOnlyDictionary<Guid, decimal> NormalizePreferences(Dictionary<Guid, decimal> preferences)
    {
        if (preferences.Count == 0)
        {
            return preferences;
        }

        var maxScore = preferences.Values.Max();
        if (maxScore > 0m)
        {
            return preferences.ToDictionary(
                pair => pair.Key,
                pair => pair.Value / maxScore);
        }

        var minScore = preferences.Values.Min();
        if (minScore < 0m)
        {
            var scale = Math.Abs(minScore);
            return preferences.ToDictionary(
                pair => pair.Key,
                pair => pair.Value / scale);
        }

        return preferences;
    }

    public static decimal CalculateKeywordScore(
        PersonalizedCandidateProfile candidate,
        IReadOnlyDictionary<Guid, decimal> keywordPreferences)
    {
        if (keywordPreferences.Count == 0)
        {
            return 0m;
        }

        var keywordIds = candidate.KeywordIds.Distinct().ToList();
        if (keywordIds.Count == 0)
        {
            return 0m;
        }

        var rawOverlap = keywordIds
            .Where(keywordPreferences.ContainsKey)
            .Sum(keywordId => keywordPreferences[keywordId]);

        if (rawOverlap == 0m)
        {
            return 0m;
        }

        var normalized = rawOverlap / (decimal)Math.Sqrt(keywordIds.Count);
        if (normalized > 1m)
        {
            return 1m;
        }

        if (normalized < -1m)
        {
            return -1m;
        }

        return normalized;
    }
}
