using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class KeywordAffinityScorerTests
{
    private static readonly RecommendationOptions DefaultOptions = new();
    private static readonly DateTime UtcNow = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SciFiGenreId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid KeywordTimeTravel = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid KeywordDream = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid KeywordWeak = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void CalculateKeywordScoreIncreasesForMatchingCandidate()
    {
        var preferences = BuildPreferencesFromFavorite([KeywordTimeTravel]);
        var candidate = CreateCandidate([KeywordTimeTravel]);

        var score = KeywordAffinityScorer.CalculateKeywordScore(candidate, preferences);

        Assert.True(score > 0m);
    }

    [Fact]
    public void CalculateKeywordScoreReturnsZeroForNonMatchingKeyword()
    {
        var preferences = BuildPreferencesFromFavorite([KeywordTimeTravel]);
        var candidate = CreateCandidate([KeywordWeak]);

        Assert.Equal(0m, KeywordAffinityScorer.CalculateKeywordScore(candidate, preferences));
    }

    [Fact]
    public void CalculateKeywordScoreIsNeutralWhenCandidateHasNoKeywords()
    {
        var preferences = BuildPreferencesFromFavorite([KeywordTimeTravel]);

        Assert.Equal(0m, KeywordAffinityScorer.CalculateKeywordScore(CreateCandidate([]), preferences));
    }

    [Fact]
    public void BuildKeywordPreferencesIgnoresSourceWithoutKeywords()
    {
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [])
        };

        Assert.Empty(KeywordAffinityScorer.BuildKeywordPreferences(signals, DefaultOptions, UtcNow));
    }

    [Fact]
    public void BuildKeywordPreferencesAllowsEmptyProfileWithoutFailure()
    {
        Assert.Empty(KeywordAffinityScorer.BuildKeywordPreferences([], DefaultOptions, UtcNow));
    }

    [Fact]
    public void FavoriteEvidenceProducesStrongerKeywordWeightThanWatchedOnly()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
        [
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordTimeTravel]),
            CreateSignal(UserBehaviorSignalTypes.Watched, null, UtcNow, [KeywordDream], Guid.NewGuid())
        ],
            DefaultOptions,
            UtcNow);

        Assert.True(preferences[KeywordTimeTravel] > preferences[KeywordDream]);
    }

    [Fact]
    public void HighRatingEvidenceProducesStrongerKeywordWeightThanWatchedOnly()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
        [
            CreateSignal(UserBehaviorSignalTypes.Rating, 9, UtcNow, [KeywordTimeTravel]),
            CreateSignal(UserBehaviorSignalTypes.Watched, null, UtcNow, [KeywordDream], Guid.NewGuid())
        ],
            DefaultOptions,
            UtcNow);

        Assert.True(preferences[KeywordTimeTravel] > preferences[KeywordDream]);
    }

    [Fact]
    public void LowRatingContributesNoPositiveKeywordAffinity()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.Rating, 3, UtcNow, [KeywordTimeTravel])],
            DefaultOptions,
            UtcNow);

        Assert.Empty(preferences);
    }

    [Fact]
    public void TvFollowContributesPositiveKeywordAffinity()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.TvFollow, null, UtcNow, [KeywordTimeTravel])],
            DefaultOptions,
            UtcNow);

        Assert.Contains(KeywordTimeTravel, preferences.Keys);
        Assert.True(preferences[KeywordTimeTravel] > 0m);
    }

    [Fact]
    public void SearchContributesWeakKeywordAffinity()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
        [
            CreateSignal(UserBehaviorSignalTypes.Search, null, null, [KeywordTimeTravel]),
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordDream], Guid.NewGuid())
        ],
            DefaultOptions,
            UtcNow);

        Assert.True(preferences[KeywordDream] > preferences[KeywordTimeTravel]);
    }

    [Fact]
    public void RecencyMultiplierAffectsKeywordEvidence()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
        [
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-2), [KeywordTimeTravel]),
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-60), [KeywordDream], Guid.NewGuid())
        ],
            DefaultOptions,
            UtcNow);

        Assert.True(preferences[KeywordTimeTravel] > preferences[KeywordDream]);
    }

    [Fact]
    public void RepeatedKeywordAcrossSourceTitlesStrengthensAffinity()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
        [
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordTimeTravel, KeywordDream]),
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordTimeTravel], Guid.NewGuid())
        ],
            DefaultOptions,
            UtcNow);

        Assert.True(preferences[KeywordTimeTravel] > preferences[KeywordDream]);
    }

    [Fact]
    public void DuplicateKeywordWithinTitleDoesNotMultiplyContribution()
    {
        var single = BuildPreferencesFromFavorite([KeywordTimeTravel]);
        var duplicated = KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordTimeTravel, KeywordTimeTravel])],
            DefaultOptions,
            UtcNow);

        Assert.Equal(single[KeywordTimeTravel], duplicated[KeywordTimeTravel]);
    }

    [Fact]
    public void SourceKeywordCountNormalizationPreventsManyKeywordSourceFromDominating()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow,
                [KeywordTimeTravel, KeywordDream, KeywordWeak, Guid.NewGuid(), Guid.NewGuid()])],
            DefaultOptions,
            UtcNow);

        Assert.Equal(5, preferences.Count);
        Assert.Equal(preferences[KeywordTimeTravel], preferences[KeywordDream]);
        Assert.True(preferences.Values.All(weight => weight > 0m && weight <= 1m));
    }

    [Fact]
    public void CandidateKeywordCountNormalizationPreventsKeywordRichCandidateFromDominating()
    {
        var preferences = BuildPreferencesFromFavorite([KeywordTimeTravel]);
        var sparseCandidateScore = KeywordAffinityScorer.CalculateKeywordScore(
            CreateCandidate([KeywordTimeTravel]),
            preferences);
        var denseCandidateScore = KeywordAffinityScorer.CalculateKeywordScore(
            CreateCandidate([KeywordTimeTravel, KeywordDream, KeywordWeak, Guid.NewGuid(), Guid.NewGuid()]),
            preferences);

        Assert.True(sparseCandidateScore > denseCandidateScore);
    }

    [Fact]
    public void KeywordScoreRemainsBoundedBetweenZeroAndOne()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
        [
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordTimeTravel]),
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordDream], Guid.NewGuid()),
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordWeak], Guid.NewGuid())
        ],
            DefaultOptions,
            UtcNow);

        var score = KeywordAffinityScorer.CalculateKeywordScore(
            CreateCandidate([KeywordTimeTravel, KeywordDream, KeywordWeak]),
            preferences);

        Assert.InRange(score, 0m, 1m);
    }

    [Fact]
    public void KeywordScoringHandlesZeroAndEmptyCasesWithoutInvalidNumbers()
    {
        Assert.Equal(0m, KeywordAffinityScorer.CalculateKeywordScore(CreateCandidate([]), new Dictionary<Guid, decimal>()));
        Assert.Equal(0m, KeywordAffinityScorer.CalculateKeywordScore(CreateCandidate([KeywordTimeTravel]), new Dictionary<Guid, decimal>()));
    }

    [Fact]
    public void MovieSourceKeywordAffinityAppliesToTvCandidate()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, [KeywordTimeTravel], contentType: "movie")],
            DefaultOptions,
            UtcNow);

        var tvCandidate = CreateCandidate([KeywordTimeTravel], contentType: "tv");
        Assert.True(KeywordAffinityScorer.CalculateKeywordScore(tvCandidate, preferences) > 0m);
    }

    [Fact]
    public void TvSourceKeywordAffinityAppliesToMovieCandidate()
    {
        var preferences = KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.TvFollow, null, UtcNow, [KeywordTimeTravel], contentType: "tv")],
            DefaultOptions,
            UtcNow);

        var movieCandidate = CreateCandidate([KeywordTimeTravel], contentType: "movie");
        Assert.True(KeywordAffinityScorer.CalculateKeywordScore(movieCandidate, preferences) > 0m);
    }

    [Fact]
    public void StrongGenreMatchWithoutKeywordBeatsWeakKeywordOnlyMatch()
    {
        var options = new RecommendationOptions
        {
            PersonalizedGenreWeight = 0.45,
            PersonalizedKeywordWeight = 0.15,
            PersonalizedBehaviorWeight = 0,
            PersonalizedPopularityWeight = 0,
            PersonalizedRecencyWeight = 0
        };

        var genrePreferences = new Dictionary<Guid, (decimal Score, string Name)>
        {
            [SciFiGenreId] = (1m, "Science Fiction")
        };
        var keywordPreferences = KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.Search, null, null, [KeywordWeak])],
            options,
            UtcNow);

        var genreMatch = CreateCandidate([KeywordDream], [SciFiGenreId]);
        var keywordOnly = CreateCandidate([KeywordWeak], []);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [genreMatch, keywordOnly],
            [],
            genrePreferences,
            keywordPreferences,
            options,
            UtcNow);

        Assert.Equal(genreMatch.Id, scored[0].Candidate.Id);
    }

    [Fact]
    public void SameGenreCandidateWithStrongKeywordMatchRanksAboveKeywordlessPeer()
    {
        var options = new RecommendationOptions
        {
            PersonalizedGenreWeight = 0.45,
            PersonalizedKeywordWeight = 0.15,
            PersonalizedBehaviorWeight = 0,
            PersonalizedPopularityWeight = 0,
            PersonalizedRecencyWeight = 0
        };

        var genrePreferences = new Dictionary<Guid, (decimal Score, string Name)>
        {
            [SciFiGenreId] = (1m, "Science Fiction")
        };
        var keywordPreferences = BuildPreferencesFromFavorite([KeywordTimeTravel]);

        var withKeyword = CreateCandidate([KeywordTimeTravel], [SciFiGenreId]);
        var withoutKeyword = CreateCandidate([KeywordDream], [SciFiGenreId]);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [withKeyword, withoutKeyword],
            [],
            genrePreferences,
            keywordPreferences,
            options,
            UtcNow);

        Assert.Equal(withKeyword.Id, scored[0].Candidate.Id);
    }

    [Fact]
    public void StrongGenreAndKeywordCandidateRanksAboveGenreOnlyPeer()
    {
        var options = new RecommendationOptions
        {
            PersonalizedGenreWeight = 0.45,
            PersonalizedKeywordWeight = 0.15,
            PersonalizedBehaviorWeight = 0,
            PersonalizedPopularityWeight = 0,
            PersonalizedRecencyWeight = 0
        };

        var genrePreferences = new Dictionary<Guid, (decimal Score, string Name)>
        {
            [SciFiGenreId] = (1m, "Science Fiction")
        };
        var keywordPreferences = BuildPreferencesFromFavorite([KeywordTimeTravel]);

        var strongBoth = CreateCandidate([KeywordTimeTravel], [SciFiGenreId]);
        var genreOnly = CreateCandidate([KeywordDream], [SciFiGenreId]);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [strongBoth, genreOnly],
            [],
            genrePreferences,
            keywordPreferences,
            options,
            UtcNow);

        Assert.Equal(strongBoth.Id, scored[0].Candidate.Id);
    }

    private static IReadOnlyDictionary<Guid, decimal> BuildPreferencesFromFavorite(IReadOnlyList<Guid> keywordIds) =>
        KeywordAffinityScorer.BuildKeywordPreferences(
            [CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow, keywordIds)],
            DefaultOptions,
            UtcNow);

    private static UserBehaviorSignal CreateSignal(
        string signalType,
        int? rating,
        DateTime? signalAtUtc,
        IReadOnlyList<Guid> keywordIds,
        Guid? contentId = null,
        string contentType = "movie") =>
        new(
            contentId ?? Guid.NewGuid(),
            contentType,
            signalType,
            "Interstellar",
            rating,
            signalAtUtc,
            [SciFiGenreId],
            new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" },
            [])
        {
            KeywordIds = keywordIds
        };

    private static PersonalizedCandidateProfile CreateCandidate(
        IReadOnlyList<Guid> keywordIds,
        IReadOnlyList<Guid>? genreIds = null,
        string contentType = "movie") =>
        new(
            Guid.NewGuid(),
            contentType,
            "Candidate",
            null,
            null,
            null,
            null,
            new DateOnly(2014, 1, 1),
            8m,
            100,
            2014,
            genreIds ?? [],
            genreIds?.ToDictionary(id => id, _ => "Science Fiction") ?? new Dictionary<Guid, string>(),
            [],
            null)
        {
            KeywordIds = keywordIds
        };
}
