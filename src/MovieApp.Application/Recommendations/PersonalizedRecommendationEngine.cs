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
        if (maxScore > 0m)
        {
            return preferences.ToDictionary(
                pair => pair.Key,
                pair => (pair.Value.Score / maxScore, pair.Value.Name));
        }

        var minScore = preferences.Values.Min(item => item.Score);
        if (minScore < 0m)
        {
            var scale = Math.Abs(minScore);
            return preferences.ToDictionary(
                pair => pair.Key,
                pair => (pair.Value.Score / scale, pair.Value.Name));
        }

        return preferences;
    }

    public static List<Guid> SelectPositiveGenreIds(
        IReadOnlyDictionary<Guid, (decimal Score, string Name)> genrePreferences) =>
        genrePreferences
            .Where(pair => pair.Value.Score > 0m)
            .OrderByDescending(pair => pair.Value.Score)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .ToList();

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
        var behaviorSignals = SelectStrongestSignals(positiveSignals, options, utcNow);

        var maxVoteCount = candidates.Count == 0 ? 1 : Math.Max(1, candidates.Max(candidate => candidate.VoteCount));
        var currentYear = utcNow.Year;

        return candidates
            .Select(candidate =>
            {
                var genreScore = CalculateGenrePreferenceScore(candidate, genrePreferences);
                var keywordScore = KeywordAffinityScorer.CalculateKeywordScore(candidate, keywordPreferences);
                var behavior = CalculateBehaviorSimilarity(candidate, behaviorSignals, options);
                var popularityScore = CalculatePopularityScore(candidate, maxVoteCount);
                var recencyScore = SimilarityEngine.CalculateYearProximity(currentYear, candidate.Year);

                var score = SimilarityEngine.RoundScore(
                    genreScore * (decimal)options.PersonalizedGenreWeight +
                    keywordScore * (decimal)options.PersonalizedKeywordWeight +
                    behavior.Score * (decimal)options.PersonalizedBehaviorWeight +
                    popularityScore * (decimal)options.PersonalizedPopularityWeight +
                    recencyScore * (decimal)options.PersonalizedRecencyWeight);

                return new ScoredRecommendation(
                    candidate,
                    score,
                    BuildReason(
                        candidate,
                        genrePreferences,
                        genreScore,
                        keywordScore,
                        behavior.Score,
                        behavior.Signal,
                        options));
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
        int? maxPerGenre = null)
    {
        var genreCap = maxPerGenre ?? options.DiversityMaxPerGenre;
        var collectionCap = options.DiversityMaxPerCollection;
        var franchiseCap = options.DiversityMaxPerFranchiseFamily;
        var selected = new List<ScoredRecommendation>();
        var genreCounts = new Dictionary<Guid, int>();
        var collectionCounts = new Dictionary<int, int>();
        var franchiseCounts = new Dictionary<Guid, int>();

        foreach (var recommendation in recommendations)
        {
            var dominantGenres = recommendation.Candidate.GenreIds.Take(2).ToList();
            if (genreCap > 0 &&
                dominantGenres.Count > 0 &&
                dominantGenres.Any(genreId =>
                    genreCounts.TryGetValue(genreId, out var count) && count >= genreCap))
            {
                continue;
            }

            if (collectionCap > 0 &&
                recommendation.Candidate.TmdbCollectionId is int collectionId &&
                collectionCounts.TryGetValue(collectionId, out var collectionCount) &&
                collectionCount >= collectionCap)
            {
                continue;
            }

            var franchiseKeywordIds = recommendation.Candidate.FranchiseKeywordIds;
            if (franchiseCap > 0 &&
                franchiseKeywordIds.Count > 0 &&
                franchiseKeywordIds.Any(keywordId =>
                    franchiseCounts.TryGetValue(keywordId, out var franchiseCount) &&
                    franchiseCount >= franchiseCap))
            {
                continue;
            }

            selected.Add(recommendation);

            foreach (var genreId in dominantGenres)
            {
                genreCounts[genreId] = genreCounts.GetValueOrDefault(genreId) + 1;
            }

            if (collectionCap > 0 && recommendation.Candidate.TmdbCollectionId is int selectedCollectionId)
            {
                collectionCounts[selectedCollectionId] =
                    collectionCounts.GetValueOrDefault(selectedCollectionId) + 1;
            }

            if (franchiseCap > 0)
            {
                foreach (var keywordId in franchiseKeywordIds.Distinct())
                {
                    franchiseCounts[keywordId] = franchiseCounts.GetValueOrDefault(keywordId) + 1;
                }
            }
        }

        return selected;
    }

    public static IReadOnlyList<RecommendationItem> ApplyDiversity(
        IReadOnlyList<RecommendationItem> items,
        RecommendationOptions options)
    {
        var scored = items
            .Select(item => new ScoredRecommendation(
                new PersonalizedCandidateProfile(
                    item.Id,
                    item.Type,
                    item.Title,
                    item.OriginalTitle,
                    item.Overview,
                    item.PosterUrl,
                    item.BackdropUrl,
                    item.ReleaseDate,
                    item.VoteAverage,
                    item.VoteCount,
                    item.Year,
                    item.DiversityGenreIds ?? [],
                    new Dictionary<Guid, string>(),
                    [],
                    item.TmdbCollectionId)
                {
                    FranchiseKeywordIds = item.FranchiseKeywordIds ?? []
                },
                item.Score,
                item.Reason))
            .ToList();

        return ApplyDiversity(scored, options)
            .Select(recommendation => new RecommendationItem(
                recommendation.Candidate.Id,
                recommendation.Candidate.Type,
                recommendation.Candidate.Title,
                recommendation.Candidate.OriginalTitle,
                recommendation.Candidate.Overview,
                recommendation.Candidate.PosterUrl,
                recommendation.Candidate.BackdropUrl,
                recommendation.Candidate.ReleaseDate,
                recommendation.Candidate.VoteAverage,
                recommendation.Candidate.VoteCount,
                recommendation.Candidate.Year,
                recommendation.Score,
                recommendation.Reason,
                recommendation.Candidate.GenreIds,
                recommendation.Candidate.TmdbCollectionId,
                recommendation.Candidate.FranchiseKeywordIds))
            .ToList();
    }

    /// <summary>
    /// Home rail: hard collection, genre, and franchise caps, then a light primary-genre
    /// interleave. When caps leave fewer than <paramref name="sectionSize"/> titles, the
    /// remainder is filled from later items in this same scored pool that still pass the caps.
    /// </summary>
    public static IReadOnlyList<RecommendationItem> SelectHomeRecommended(
        IReadOnlyList<RecommendationItem> scoredPool,
        RecommendationOptions options,
        int sectionSize)
    {
        if (sectionSize <= 0 || scoredPool.Count == 0)
        {
            return [];
        }

        var capPassing = ApplyDiversity(scoredPool, options);
        var interleaved = InterleavePrimaryGenres(capPassing);
        var selected = new List<RecommendationItem>(Math.Min(sectionSize, interleaved.Count));
        var seen = new HashSet<(Guid Id, string Type)>();

        foreach (var item in interleaved)
        {
            if (!seen.Add((item.Id, item.Type)))
            {
                continue;
            }

            selected.Add(item);
            if (selected.Count == sectionSize)
            {
                break;
            }
        }

        return selected;
    }

    private static List<RecommendationItem> InterleavePrimaryGenres(IReadOnlyList<RecommendationItem> items)
    {
        var result = new List<RecommendationItem>(items.Count);
        var used = new bool[items.Count];
        var remaining = items.Count;

        while (remaining > 0)
        {
            var chosen = ChooseNextPrimaryGenreIndex(items, used, result);
            if (chosen < 0)
            {
                break;
            }

            used[chosen] = true;
            remaining--;
            result.Add(items[chosen]);
        }

        return result;
    }

    private static int ChooseNextPrimaryGenreIndex(
        IReadOnlyList<RecommendationItem> items,
        bool[] used,
        List<RecommendationItem> selected)
    {
        var lastGenre = selected.Count == 0 ? null : PrimaryGenre(selected[^1]);

        for (var index = 0; index < items.Count; index++)
        {
            if (used[index])
            {
                continue;
            }

            var genre = PrimaryGenre(items[index]);
            var samePrimary = lastGenre.HasValue && genre.HasValue && genre.Value == lastGenre.Value;
            if (!samePrimary || !LaterDifferentPrimaryExists(items, used, index, lastGenre!.Value))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool LaterDifferentPrimaryExists(
        IReadOnlyList<RecommendationItem> items,
        bool[] used,
        int afterIndex,
        Guid lastGenre)
    {
        for (var index = afterIndex + 1; index < items.Count; index++)
        {
            if (used[index])
            {
                continue;
            }

            var genre = PrimaryGenre(items[index]);
            if (!genre.HasValue || genre.Value != lastGenre)
            {
                return true;
            }
        }

        return false;
    }

    private static Guid? PrimaryGenre(RecommendationItem item) =>
        item.DiversityGenreIds is { Count: > 0 } ? item.DiversityGenreIds[0] : null;

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

    private static List<UserBehaviorSignal> SelectStrongestSignals(
        List<UserBehaviorSignal> positiveSignals,
        RecommendationOptions options,
        DateTime utcNow) =>
        positiveSignals
            .OrderByDescending(signal => RecommendationSignalScoring.GetSignalContribution(signal, options, utcNow))
            .ThenByDescending(signal => signal.SignalAtUtc ?? DateTime.MinValue)
            .Take(10)
            .ToList();

    private static (decimal Score, UserBehaviorSignal? Signal) CalculateBehaviorSimilarity(
        PersonalizedCandidateProfile candidate,
        List<UserBehaviorSignal> positiveSignals,
        RecommendationOptions options)
    {
        if (positiveSignals.Count == 0)
        {
            return (0m, null);
        }

        decimal bestScore = 0m;
        UserBehaviorSignal? bestSignal = null;

        foreach (var signal in positiveSignals)
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
            if (score > bestScore)
            {
                bestScore = score;
                bestSignal = signal;
            }
        }

        return (bestScore, bestSignal);
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
        decimal genreScore,
        decimal keywordScore,
        decimal behaviorScore,
        UserBehaviorSignal? bestBehaviorSignal,
        RecommendationOptions options)
    {
        var genreWeighted = genreScore * (decimal)options.PersonalizedGenreWeight;
        var keywordWeighted = Math.Max(0m, keywordScore) * (decimal)options.PersonalizedKeywordWeight;
        var behaviorWeighted = behaviorScore * (decimal)options.PersonalizedBehaviorWeight;
        var topGenre = candidate.GenreIds
            .Take(2)
            .Where(genrePreferences.ContainsKey)
            .Select(genreId => (genreId, genrePreferences[genreId]))
            .Where(item => item.Item2.Score > 0m)
            .OrderByDescending(item => item.Item2.Score)
            .FirstOrDefault();

        if (genreWeighted > 0m &&
            genreWeighted >= keywordWeighted &&
            genreWeighted >= behaviorWeighted &&
            genreScore >= MinGenreReasonScore &&
            topGenre != default &&
            topGenre.Item2.Score >= MinGenrePreferenceScore &&
            !string.IsNullOrWhiteSpace(topGenre.Item2.Name))
        {
            return $"Because you liked {topGenre.Item2.Name}";
        }

        if (bestBehaviorSignal is not null &&
            (behaviorWeighted > 0m || keywordWeighted > 0m))
        {
            return ReasonForSignal(bestBehaviorSignal);
        }

        return bestBehaviorSignal is null
            ? "Popular in your favorite genres"
            : ReasonForSignal(bestBehaviorSignal);
    }

    private static string ReasonForSignal(UserBehaviorSignal signal)
    {
        if (signal.SignalType == UserBehaviorSignalTypes.Rating &&
            signal.RatingScore >= 8 &&
            !string.IsNullOrWhiteSpace(signal.Title))
        {
            return $"Because you rated {signal.Title} highly";
        }

        if (signal.SignalType == UserBehaviorSignalTypes.Watched &&
            !string.IsNullOrWhiteSpace(signal.Title))
        {
            return $"Because you watched {signal.Title}";
        }

        if (signal.SignalType == UserBehaviorSignalTypes.Watchlist)
        {
            return "From your watchlist";
        }

        if (signal.SignalType == UserBehaviorSignalTypes.Favorite ||
            signal.SignalType == UserBehaviorSignalTypes.TvFollow)
        {
            return "Based on your favorites";
        }

        return "Popular in your favorite genres";
    }
}
