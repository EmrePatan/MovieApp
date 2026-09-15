using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class PersonalizedRecommendationEngineTests
{
    private static readonly RecommendationOptions DefaultOptions = new();
    private static readonly Guid SciFiGenreId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime UtcNow = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildGenrePreferencesUsesRatingFavoriteAndSearchSignals()
    {
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Rating, 10, UtcNow.AddDays(-2)),
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-2)),
            CreateSignal(UserBehaviorSignalTypes.Search, null, null)
        };

        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions, UtcNow);

        Assert.True(preferences.ContainsKey(SciFiGenreId));
        Assert.Equal(1m, preferences[SciFiGenreId].Score);
    }

    [Fact]
    public void ScoreCandidatesDoesNotUseTertiaryGenreForReason()
    {
        var comedyGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var dramaGenreId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var comedyGenre = new Dictionary<Guid, string> { [comedyGenreId] = "Comedy" };
        var mixedGenreNames = new Dictionary<Guid, string>
        {
            [dramaGenreId] = "Drama",
            [SciFiGenreId] = "Science Fiction",
            [comedyGenreId] = "Comedy",
        };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-2), genreIds: [comedyGenreId], genreNames: comedyGenre)
        };
        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions, UtcNow);
        var candidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [dramaGenreId, SciFiGenreId, comedyGenreId],
            mixedGenreNames,
            8.5m,
            1000,
            null);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [candidate],
            signals,
            preferences,
            new Dictionary<Guid, decimal>(),
            DefaultOptions,
            UtcNow);

        Assert.DoesNotContain("Comedy", scored[0].Reason ?? string.Empty);
    }

    [Fact]
    public void ScoreCandidatesRequiresMeaningfulGenreOverlapForReason()
    {
        var comedyGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var comedyGenre = new Dictionary<Guid, string> { [comedyGenreId] = "Comedy" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-2), genreIds: [comedyGenreId], genreNames: comedyGenre)
        };
        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions, UtcNow);
        var candidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [
                comedyGenreId,
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
            ],
            comedyGenre,
            8.5m,
            1000,
            null);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [candidate],
            signals,
            preferences,
            new Dictionary<Guid, decimal>(),
            DefaultOptions,
            UtcNow);

        Assert.DoesNotContain("Because you liked Comedy", scored[0].Reason ?? string.Empty);
    }

    [Fact]
    public void ScoreCandidatesRanksGenreAlignedContentHigher()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-2))
        };
        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions, UtcNow);

        var candidates = new List<PersonalizedCandidateProfile>
        {
            CreateCandidate(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), [SciFiGenreId], sciFiGenre, 8.5m, 1000, null),
            CreateCandidate(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), [Guid.Parse("99999999-9999-9999-9999-999999999999")], new Dictionary<Guid, string>(), 9.5m, 2000, null)
        };

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            candidates,
            signals,
            preferences,
            new Dictionary<Guid, decimal>(),
            DefaultOptions,
            UtcNow);

        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), scored[0].Candidate.Id);
        Assert.False(string.IsNullOrWhiteSpace(scored[0].Reason));
    }

    [Fact]
    public void ScoreCandidatesDoesNotUsePersonOverlap()
    {
        var personId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var signals = new List<UserBehaviorSignal>
        {
            new(
                Guid.NewGuid(),
                "movie",
                UserBehaviorSignalTypes.Favorite,
                "Source",
                null,
                UtcNow,
                [],
                new Dictionary<Guid, string>(),
                [personId])
        };
        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions, UtcNow);

        var withPerson = CreateCandidate(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            [],
            new Dictionary<Guid, string>(),
            8m,
            100,
            null,
            [personId]);
        var withoutPerson = CreateCandidate(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            [],
            new Dictionary<Guid, string>(),
            8m,
            100,
            null,
            []);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [withPerson, withoutPerson],
            signals,
            preferences,
            new Dictionary<Guid, decimal>(),
            DefaultOptions,
            UtcNow);

        var withPersonScore = scored.Single(item => item.Candidate.Id == withPerson.Id).Score;
        var withoutPersonScore = scored.Single(item => item.Candidate.Id == withoutPerson.Id).Score;
        Assert.Equal(withPersonScore, withoutPersonScore);
    }

    [Fact]
    public void ApplyDiversityLimitsSameGenreDominance()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var recommendations = Enumerable.Range(0, 6)
            .Select(index => new ScoredRecommendation(
                CreateCandidate(Guid.NewGuid(), [SciFiGenreId], sciFiGenre, 8m, 100 + index, null),
                1m,
                "Because you liked Science Fiction"))
            .ToList();

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(recommendations, DefaultOptions, maxPerGenre: 2);

        Assert.Equal(6, diversified.Count);
        Assert.Equal(2, diversified.Take(2).Count(item => item.Candidate.GenreIds.Contains(SciFiGenreId)));
        Assert.Contains(
            diversified.Skip(2),
            item => item.Candidate.GenreIds.Contains(SciFiGenreId));
    }

    [Fact]
    public void ScoreCandidatesPrefersCloserCatalogVoteAverageForBehavior()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(
                UserBehaviorSignalTypes.Favorite,
                null,
                UtcNow.AddDays(-2),
                catalogVoteAverage: 8.0m,
                catalogYear: 2014)
        };

        var closeVoteCandidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [SciFiGenreId],
            sciFiGenre,
            8.5m,
            1000,
            null,
            year: 2014);
        var distantVoteCandidate = CreateCandidate(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [SciFiGenreId],
            sciFiGenre,
            2.0m,
            1000,
            null,
            year: 2014);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [closeVoteCandidate, distantVoteCandidate],
            signals,
            new Dictionary<Guid, (decimal Score, string Name)>(),
            new Dictionary<Guid, decimal>(),
            BehaviorOnlyOptions(),
            UtcNow);

        Assert.Equal(closeVoteCandidate.Id, scored[0].Candidate.Id);
        Assert.True(scored[0].Score > scored[1].Score);
    }

    [Fact]
    public void ScoreCandidatesUsesSourceCatalogYearForBehavior()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(
                UserBehaviorSignalTypes.Favorite,
                null,
                UtcNow.AddDays(-2),
                catalogVoteAverage: 8.0m,
                catalogYear: 2014)
        };

        var closeYearCandidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [SciFiGenreId],
            sciFiGenre,
            8.0m,
            1000,
            null,
            year: 2015);
        var distantYearCandidate = CreateCandidate(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [SciFiGenreId],
            sciFiGenre,
            8.0m,
            1000,
            null,
            year: 1960);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [closeYearCandidate, distantYearCandidate],
            signals,
            new Dictionary<Guid, (decimal Score, string Name)>(),
            new Dictionary<Guid, decimal>(),
            BehaviorOnlyOptions(),
            UtcNow);

        Assert.Equal(closeYearCandidate.Id, scored[0].Candidate.Id);
        Assert.True(scored[0].Score > scored[1].Score);
    }

    [Fact]
    public void ScoreCandidatesDoesNotUseUserRatingScoreAsCatalogVoteAverage()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(
                UserBehaviorSignalTypes.Rating,
                10,
                UtcNow.AddDays(-2),
                catalogVoteAverage: 4.0m,
                catalogYear: 2014)
        };

        var catalogCloseCandidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [SciFiGenreId],
            sciFiGenre,
            4.5m,
            1000,
            null,
            year: 2014);
        var userRatingCloseCandidate = CreateCandidate(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [SciFiGenreId],
            sciFiGenre,
            9.5m,
            1000,
            null,
            year: 2014);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [catalogCloseCandidate, userRatingCloseCandidate],
            signals,
            new Dictionary<Guid, (decimal Score, string Name)>(),
            new Dictionary<Guid, decimal>(),
            BehaviorOnlyOptions(),
            UtcNow);

        Assert.Equal(catalogCloseCandidate.Id, scored[0].Candidate.Id);
    }

    [Fact]
    public void ScoreCandidatesTreatsMissingSourceYearAsNeutralForBehavior()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(
                UserBehaviorSignalTypes.Favorite,
                null,
                UtcNow.AddDays(-2),
                catalogVoteAverage: 8.0m,
                catalogYear: null)
        };

        var recentYearCandidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [SciFiGenreId],
            sciFiGenre,
            8.0m,
            1000,
            null,
            year: 2024);
        var oldYearCandidate = CreateCandidate(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [SciFiGenreId],
            sciFiGenre,
            8.0m,
            1000,
            null,
            year: 1970);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [recentYearCandidate, oldYearCandidate],
            signals,
            new Dictionary<Guid, (decimal Score, string Name)>(),
            new Dictionary<Guid, decimal>(),
            BehaviorOnlyOptions(),
            UtcNow);

        Assert.Equal(recentYearCandidate.Id, scored[0].Candidate.Id);
        Assert.Equal(scored[0].Score, scored[1].Score);
    }

    [Fact]
    public void ScoreCandidatesHandlesMissingCatalogMetadataSafely()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-2))
        };

        var candidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [SciFiGenreId],
            sciFiGenre,
            8.0m,
            1000,
            null);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [candidate],
            signals,
            new Dictionary<Guid, (decimal Score, string Name)>(),
            new Dictionary<Guid, decimal>(),
            BehaviorOnlyOptions(),
            UtcNow);

        Assert.Single(scored);
        Assert.True(scored[0].Score >= 0m);
    }

    [Fact]
    public void ApplyDiversityLimitsSameCollectionAndFillsRemainingRankedCandidates()
    {
        var collectionId = 42;
        var recommendations = Enumerable.Range(0, 5)
            .Select(index => new ScoredRecommendation(
                CreateCandidate(Guid.NewGuid(), [], new Dictionary<Guid, string>(), 8m, 100 + index, collectionId),
                1m - (index * 0.01m),
                null))
            .ToList();

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(recommendations, DefaultOptions, maxPerGenre: 3);

        Assert.Equal(5, diversified.Count);
        Assert.Equal(2, diversified.Take(2).Count(item => item.Candidate.TmdbCollectionId == collectionId));
        Assert.Equal(3, diversified.Skip(2).Count(item => item.Candidate.TmdbCollectionId == collectionId));
    }

    private static RecommendationOptions BehaviorOnlyOptions() =>
        new()
        {
            PersonalizedGenreWeight = 0,
            PersonalizedKeywordWeight = 0,
            PersonalizedBehaviorWeight = 1,
            PersonalizedPopularityWeight = 0,
            PersonalizedRecencyWeight = 0
        };

    private static UserBehaviorSignal CreateSignal(
        string signalType,
        int? rating,
        DateTime? signalAtUtc,
        decimal catalogVoteAverage = 0m,
        int? catalogYear = null,
        IReadOnlyList<Guid>? genreIds = null,
        IReadOnlyDictionary<Guid, string>? genreNames = null) =>
        new(
            Guid.NewGuid(),
            "movie",
            signalType,
            "Interstellar",
            rating,
            signalAtUtc,
            genreIds ?? [SciFiGenreId],
            genreNames ?? new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" },
            [])
        {
            CatalogVoteAverage = catalogVoteAverage,
            CatalogYear = catalogYear
        };

    private static PersonalizedCandidateProfile CreateCandidate(
        Guid id,
        IReadOnlyList<Guid> genreIds,
        IReadOnlyDictionary<Guid, string> genreNames,
        decimal voteAverage,
        int voteCount,
        int? tmdbCollectionId,
        IReadOnlyList<Guid>? personIds = null,
        int year = 2014) =>
        new(
            id,
            "movie",
            $"Title-{id}",
            null,
            null,
            null,
            null,
            new DateOnly(year, 1, 1),
            voteAverage,
            voteCount,
            year,
            genreIds,
            genreNames,
            personIds ?? [],
            tmdbCollectionId);
}
