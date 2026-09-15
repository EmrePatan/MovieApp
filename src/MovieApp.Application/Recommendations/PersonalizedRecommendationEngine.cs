using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Recommendations;

public static class PersonalizedRecommendationEngine
{
    private const decimal MinGenreReasonScore = 0.25m;
    private const decimal MinGenrePreferenceScore = 0.35m;
    public static IReadOnlyDictionary<Guid, (decimal Score, string Name)> BuildGenrePreferences(
        IReadOnlyList<UserBehaviorSignal> signals,
        RecommendationOptions options,
        DateTime utcNow)
    {
        var preferences = new Dictionary<Guid, (decimal Score, string Name)>();

        foreach (var signal in signals)
        {
            var contribution = RecommendationSignalScoring.GetSignalContribution(signal, options, utcNow);
            if (contribution == 0m)
            {
                continue;
            }

            foreach (var genreId in signal.GenreIds)
            {
                signal.GenreNames.TryGetValue(genreId, out var genreName);
                if (preferences.TryGetValue(genreId, out var existing))
                {
                    preferences[genreId] = (existing.Score + contribution, existing.Name ?? genreName ?? "Unknown");
                }
                else
                {
                    preferences[genreId] = (contribution, genreName ?? "Unknown");
                }
            }
        }

        if (preferences.Count == 0)
        {
            return preferences;
        }

        var maxScore = preferences.Values.Max(item => item.Score);
        if (maxScore <= 0m)
        {
            return preferences;
        }

        return preferences.ToDictionary(
            pair => pair.Key,
            pair => (pair.Value.Score / maxScore, pair.Value.Name));
    }

    public static IReadOnlyList<ScoredRecommendation> ScoreCandidates(
        IReadOnlyList<PersonalizedCandidateProfile> candidates,
        IReadOnlyList<UserBehaviorSignal> signals,
        IReadOnlyDictionary<Guid, (decimal Score, string Name)> genrePreferences,
        IReadOnlyDictionary<Guid, decimal> keywordPreferences,
        RecommendationOptions options,
        DateTime utcNow)
    {
        var positiveSignals = signals
            .Where(signal => RecommendationSignalScoring.GetSignalContribution(signal, options, utcNow) > 0m)
            .ToList();

        var maxVoteCount = candidates.Count == 0 ? 1 : Math.Max(1, candidates.Max(candidate => candidate.VoteCount));
        var currentYear = utcNow.Year;

        return candidates
            .Select(candidate =>
            {
                var genreScore = CalculateGenrePreferenceScore(candidate, genrePreferences);
                var keywordScore = KeywordAffinityScorer.CalculateKeywordScore(candidate, keywordPreferences);
                var behaviorScore = CalculateBehaviorSimilarity(candidate, positiveSignals, options);
                var popularityScore = CalculatePopularityScore(candidate, maxVoteCount);
                var recencyScore = SimilarityEngine.CalculateYearProximity(currentYear, candidate.Year);

                var score = SimilarityEngine.RoundScore(
                    genreScore * (decimal)options.PersonalizedGenreWeight +
                    keywordScore * (decimal)options.PersonalizedKeywordWeight +
                    behaviorScore * (decimal)options.PersonalizedBehaviorWeight +
                    popularityScore * (decimal)options.PersonalizedPopularityWeight +
                    recencyScore * (decimal)options.PersonalizedRecencyWeight);

                return new ScoredRecommendation(
                    candidate,
                    score,
                    BuildReason(candidate, genrePreferences, positiveSignals, genreScore));
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Candidate.VoteCount)
            .ThenByDescending(item => item.Candidate.VoteAverage)
            .ThenBy(item => item.Candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ScoredRecommendation> ApplyDiversity(
        IReadOnlyList<ScoredRecommendation> recommendations,
        RecommendationOptions options,
        int maxPerGenre = 3)
    {
        var passOne = ApplyDiversityPass(recommendations, maxPerGenre, options.DiversityMaxPerCollection);
        var selectedKeys = passOne
            .Select(item => (item.Candidate.Id, item.Candidate.Type))
            .ToHashSet();

        var final = new List<ScoredRecommendation>(passOne);

        foreach (var recommendation in recommendations)
        {
            var key = (recommendation.Candidate.Id, recommendation.Candidate.Type);
            if (selectedKeys.Add(key))
            {
                final.Add(recommendation);
            }
        }

        return final;
    }

    private static List<ScoredRecommendation> ApplyDiversityPass(
        IReadOnlyList<ScoredRecommendation> recommendations,
        int maxPerGenre,
        int maxPerCollection)
    {
        var selected = new List<ScoredRecommendation>();
        var genreCounts = new Dictionary<Guid, int>();
        var collectionCounts = new Dictionary<int, int>();

        foreach (var recommendation in recommendations)
        {
            var dominantGenres = recommendation.Candidate.GenreIds.Take(2).ToList();
            if (dominantGenres.Count > 0 &&
                dominantGenres.Any(genreId =>
                    genreCounts.TryGetValue(genreId, out var count) && count >= maxPerGenre))
            {
                continue;
            }

            if (recommendation.Candidate.TmdbCollectionId is int collectionId &&
                collectionCounts.TryGetValue(collectionId, out var collectionCount) &&
                collectionCount >= maxPerCollection)
            {
                continue;
            }

            selected.Add(recommendation);

            foreach (var genreId in dominantGenres)
            {
                genreCounts[genreId] = genreCounts.GetValueOrDefault(genreId) + 1;
            }

            if (recommendation.Candidate.TmdbCollectionId is int selectedCollectionId)
            {
                collectionCounts[selectedCollectionId] =
                    collectionCounts.GetValueOrDefault(selectedCollectionId) + 1;
            }
        }

        return selected;
    }

    private static decimal CalculateGenrePreferenceScore(
        PersonalizedCandidateProfile candidate,
        IReadOnlyDictionary<Guid, (decimal Score, string Name)> genrePreferences)
    {
        if (candidate.GenreIds.Count == 0 || genrePreferences.Count == 0)
        {
            return 0m;
        }

        var total = candidate.GenreIds
            .Where(genrePreferences.ContainsKey)
            .Sum(genreId => genrePreferences[genreId].Score);

        return total / candidate.GenreIds.Count;
    }

    private static decimal CalculateBehaviorSimilarity(
        PersonalizedCandidateProfile candidate,
        List<UserBehaviorSignal> positiveSignals,
        RecommendationOptions options)
    {
        if (positiveSignals.Count == 0)
        {
            return 0m;
        }

        decimal bestScore = 0m;

        foreach (var signal in positiveSignals.Take(10))
        {
            var sourceProfile = new SimilaritySourceProfile(
                signal.ContentId,
                signal.ContentType,
                signal.Title ?? string.Empty,
                signal.GenreIds,
                signal.GenreNames,
                [],
                signal.CatalogVoteAverage,
                signal.CatalogYear);

            var candidateProfile = new SimilarityCandidateProfile(
                candidate.Id,
                candidate.Type,
                candidate.Title,
                candidate.OriginalTitle,
                candidate.Overview,
                candidate.PosterUrl,
                candidate.BackdropUrl,
                candidate.ReleaseDate,
                candidate.VoteAverage,
                candidate.VoteCount,
                candidate.Year,
                candidate.GenreIds,
                candidate.GenreNames,
                []);

            var score = SimilarityEngine.CalculateScore(sourceProfile, candidateProfile, options);
            bestScore = Math.Max(bestScore, score);
        }

        return bestScore;
    }

    private static decimal CalculatePopularityScore(PersonalizedCandidateProfile candidate, int maxVoteCount)
    {
        var ratingComponent = candidate.VoteAverage / 10m;
        var voteCountComponent = (decimal)candidate.VoteCount / maxVoteCount;
        return (ratingComponent * 0.6m) + (voteCountComponent * 0.4m);
    }

    private static string? BuildReason(
        PersonalizedCandidateProfile candidate,
        IReadOnlyDictionary<Guid, (decimal Score, string Name)> genrePreferences,
        IReadOnlyList<UserBehaviorSignal> positiveSignals,
        decimal genreScore)
    {
        var topGenre = candidate.GenreIds
            .Take(2)
            .Where(genrePreferences.ContainsKey)
            .Select(genreId => (genreId, genrePreferences[genreId]))
            .OrderByDescending(item => item.Item2.Score)
            .FirstOrDefault();

        if (genreScore >= MinGenreReasonScore &&
            topGenre != default &&
            topGenre.Item2.Score >= MinGenrePreferenceScore &&
            !string.IsNullOrWhiteSpace(topGenre.Item2.Name))
        {
            return $"Because you liked {topGenre.Item2.Name}";
        }

        var ratingSignal = positiveSignals
            .Where(signal => signal.SignalType == UserBehaviorSignalTypes.Rating && signal.RatingScore >= 8)
            .FirstOrDefault(signal => !string.IsNullOrWhiteSpace(signal.Title));

        if (ratingSignal is not null)
        {
            return $"Because you rated {ratingSignal.Title} highly";
        }

        var watchedSignal = positiveSignals
            .FirstOrDefault(signal =>
                signal.SignalType == UserBehaviorSignalTypes.Watched &&
                !string.IsNullOrWhiteSpace(signal.Title));

        if (watchedSignal is not null)
        {
            return $"Because you watched {watchedSignal.Title}";
        }

        var favoriteSignal = positiveSignals
            .FirstOrDefault(signal =>
                signal.SignalType == UserBehaviorSignalTypes.Favorite &&
                !string.IsNullOrWhiteSpace(signal.Title));

        if (favoriteSignal is not null)
        {
            return "Based on your favorites";
        }

        return "Popular in your favorite genres";
    }
}
