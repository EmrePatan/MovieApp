using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Recommendations;

public static class PersonalizedRecommendationEngine
{
    public static IReadOnlyDictionary<Guid, (decimal Score, string Name)> BuildGenrePreferences(
        IReadOnlyList<UserBehaviorSignal> signals,
        RecommendationOptions options)
    {
        var preferences = new Dictionary<Guid, (decimal Score, string Name)>();

        foreach (var signal in signals)
        {
            var contribution = GetSignalContribution(signal, options);
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
        RecommendationOptions options)
    {
        var positiveSignals = signals
            .Where(signal => GetSignalContribution(signal, options) > 0m)
            .ToList();

        var preferredPeople = positiveSignals
            .SelectMany(signal => signal.PersonIds)
            .Distinct()
            .ToHashSet();

        var maxVoteCount = candidates.Count == 0 ? 1 : Math.Max(1, candidates.Max(candidate => candidate.VoteCount));
        var currentYear = DateTime.UtcNow.Year;

        return candidates
            .Select(candidate =>
            {
                var genreScore = CalculateGenrePreferenceScore(candidate, genrePreferences);
                var personScore = SimilarityEngine.CalculateOverlapScore(
                    preferredPeople.ToList(),
                    candidate.PersonIds);
                var behaviorScore = CalculateBehaviorSimilarity(candidate, positiveSignals, options);
                var popularityScore = CalculatePopularityScore(candidate, maxVoteCount);
                var recencyScore = SimilarityEngine.CalculateYearProximity(currentYear, candidate.Year);

                var score = SimilarityEngine.RoundScore(
                    genreScore * (decimal)options.PersonalizedGenreWeight +
                    personScore * (decimal)options.PersonalizedPersonWeight +
                    behaviorScore * (decimal)options.PersonalizedBehaviorWeight +
                    popularityScore * (decimal)options.PersonalizedPopularityWeight +
                    recencyScore * (decimal)options.PersonalizedRecencyWeight);

                return new ScoredRecommendation(candidate, score, BuildReason(candidate, genrePreferences, positiveSignals));
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Candidate.VoteCount)
            .ThenByDescending(item => item.Candidate.VoteAverage)
            .ThenBy(item => item.Candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ScoredRecommendation> ApplyDiversity(
        IReadOnlyList<ScoredRecommendation> recommendations,
        int maxPerGenre = 3)
    {
        var selected = new List<ScoredRecommendation>();
        var genreCounts = new Dictionary<Guid, int>();

        foreach (var recommendation in recommendations)
        {
            var dominantGenres = recommendation.Candidate.GenreIds.Take(2).ToList();
            if (dominantGenres.Count == 0)
            {
                selected.Add(recommendation);
                continue;
            }

            if (dominantGenres.Any(genreId =>
                    genreCounts.TryGetValue(genreId, out var count) && count >= maxPerGenre))
            {
                continue;
            }

            selected.Add(recommendation);

            foreach (var genreId in dominantGenres)
            {
                genreCounts[genreId] = genreCounts.GetValueOrDefault(genreId) + 1;
            }
        }

        return selected;
    }

    private static decimal GetSignalContribution(UserBehaviorSignal signal, RecommendationOptions options)
    {
        return signal.SignalType switch
        {
            UserBehaviorSignalTypes.Rating when signal.RatingScore is not null =>
                ((signal.RatingScore.Value - 5m) / 5m) * (decimal)options.FavoriteSignalWeight,
            UserBehaviorSignalTypes.Favorite => (decimal)options.FavoriteSignalWeight,
            UserBehaviorSignalTypes.Watched => (decimal)options.WatchedSignalWeight,
            UserBehaviorSignalTypes.Watchlist => (decimal)options.WatchlistSignalWeight,
            UserBehaviorSignalTypes.Search => (decimal)options.SearchSignalWeight,
            _ => 0m
        };
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
                signal.PersonIds,
                0m,
                null);

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
                candidate.PersonIds);

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
        IReadOnlyList<UserBehaviorSignal> positiveSignals)
    {
        var topGenre = candidate.GenreIds
            .Where(genrePreferences.ContainsKey)
            .Select(genreId => (genreId, genrePreferences[genreId]))
            .OrderByDescending(item => item.Item2.Score)
            .FirstOrDefault();

        if (topGenre != default && !string.IsNullOrWhiteSpace(topGenre.Item2.Name))
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
