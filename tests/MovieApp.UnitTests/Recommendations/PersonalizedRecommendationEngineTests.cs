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
        Assert.True(diversified.Skip(2).Any(item => item.Candidate.GenreIds.Contains(SciFiGenreId)));
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

    private static UserBehaviorSignal CreateSignal(
        string signalType,
        int? rating,
        DateTime? signalAtUtc) =>
        new(
            Guid.NewGuid(),
            "movie",
            signalType,
            "Interstellar",
            rating,
            signalAtUtc,
            [SciFiGenreId],
            new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" },
            []);

    private static PersonalizedCandidateProfile CreateCandidate(
        Guid id,
        IReadOnlyList<Guid> genreIds,
        IReadOnlyDictionary<Guid, string> genreNames,
        decimal voteAverage,
        int voteCount,
        int? tmdbCollectionId,
        IReadOnlyList<Guid>? personIds = null) =>
        new(
            id,
            "movie",
            $"Title-{id}",
            null,
            null,
            null,
            null,
            new DateOnly(2014, 1, 1),
            voteAverage,
            voteCount,
            2014,
            genreIds,
            genreNames,
            personIds ?? [],
            tmdbCollectionId);
}
