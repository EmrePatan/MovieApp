using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class PersonalizedRecommendationEngineTests
{
    private static readonly RecommendationOptions DefaultOptions = new();
    private static readonly Guid SciFiGenreId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void BuildGenrePreferencesUsesRatingFavoriteAndSearchSignals()
    {
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Rating, 10, SciFiGenreId, "Science Fiction"),
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, SciFiGenreId, "Science Fiction"),
            CreateSignal(UserBehaviorSignalTypes.Search, null, SciFiGenreId, "Science Fiction")
        };

        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions);

        Assert.True(preferences.ContainsKey(SciFiGenreId));
        Assert.Equal(1m, preferences[SciFiGenreId].Score);
    }

    [Fact]
    public void ScoreCandidatesRanksGenreAlignedContentHigher()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, SciFiGenreId, "Science Fiction")
        };
        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions);

        var candidates = new List<PersonalizedCandidateProfile>
        {
            CreateCandidate(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), [SciFiGenreId], sciFiGenre, 8.5m, 1000),
            CreateCandidate(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), [Guid.Parse("99999999-9999-9999-9999-999999999999")], new Dictionary<Guid, string>(), 9.5m, 2000)
        };

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(candidates, signals, preferences, DefaultOptions);

        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), scored[0].Candidate.Id);
        Assert.False(string.IsNullOrWhiteSpace(scored[0].Reason));
    }

    [Fact]
    public void ApplyDiversityLimitsSameGenreDominance()
    {
        var sciFiGenre = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var recommendations = Enumerable.Range(0, 6)
            .Select(index => new ScoredRecommendation(
                CreateCandidate(Guid.NewGuid(), [SciFiGenreId], sciFiGenre, 8m, 100 + index),
                1m,
                "Because you liked Science Fiction"))
            .ToList();

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(recommendations, maxPerGenre: 2);

        Assert.Equal(2, diversified.Count);
    }

    private static UserBehaviorSignal CreateSignal(
        string signalType,
        int? rating,
        Guid genreId,
        string genreName) =>
        new(
            Guid.NewGuid(),
            "movie",
            signalType,
            "Interstellar",
            rating,
            [genreId],
            new Dictionary<Guid, string> { [genreId] = genreName },
            []);

    private static PersonalizedCandidateProfile CreateCandidate(
        Guid id,
        IReadOnlyList<Guid> genreIds,
        IReadOnlyDictionary<Guid, string> genreNames,
        decimal voteAverage,
        int voteCount) =>
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
            []);
}
