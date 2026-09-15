using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Recommendations;

public static class KeywordAffinityScorer
{
    public static IReadOnlyDictionary<Guid, decimal> BuildKeywordPreferences(
        IReadOnlyList<UserBehaviorSignal> signals,
        RecommendationOptions options,
        DateTime utcNow)
    {
        var preferences = new Dictionary<Guid, decimal>();

        foreach (var signal in signals)
        {
            var contribution = RecommendationSignalScoring.GetSignalContribution(signal, options, utcNow);
            if (contribution <= 0m)
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
                preferences[keywordId] = preferences.GetValueOrDefault(keywordId) + perKeywordContribution;
            }
        }

        if (preferences.Count == 0)
        {
            return preferences;
        }

        var maxScore = preferences.Values.Max();
        if (maxScore <= 0m)
        {
            return preferences;
        }

        return preferences.ToDictionary(
            pair => pair.Key,
            pair => pair.Value / maxScore);
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

        if (rawOverlap <= 0m)
        {
            return 0m;
        }

        var normalized = rawOverlap / (decimal)Math.Sqrt(keywordIds.Count);
        return normalized > 1m ? 1m : normalized;
    }
}
