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

        Assert.Equal(2, diversified.Count);
        Assert.All(diversified, item => Assert.Contains(SciFiGenreId, item.Candidate.GenreIds));
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

        Assert.Single(diversified);
        Assert.Equal(recommendations[0].Candidate.Id, diversified[0].Candidate.Id);
        Assert.Equal(collectionId, diversified[0].Candidate.TmdbCollectionId);
    }

    [Fact]
    public void ApplyDiversityHardCapsCollectionAtOneInsideTopTen()
    {
        var avengersCollection = 86311;
        var recommendations = Enumerable.Range(0, 14)
            .Select(index => new ScoredRecommendation(
                CreateCandidate(
                    Guid.NewGuid(),
                    [Guid.NewGuid()],
                    new Dictionary<Guid, string>(),
                    8m,
                    1000 - index,
                    index < 4 ? avengersCollection : 1000 + index),
                1m - (index * 0.01m),
                null))
            .ToList();

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(recommendations, DefaultOptions)
            .Take(10)
            .ToList();

        Assert.Equal(10, diversified.Count);
        Assert.Equal(1, diversified.Count(item => item.Candidate.TmdbCollectionId == avengersCollection));
        Assert.Equal(recommendations[0].Candidate.Id, diversified[0].Candidate.Id);
    }

    [Fact]
    public void ApplyDiversityHardCapsGenrePileUp()
    {
        var actionGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var comedyGenreId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var actionNames = new Dictionary<Guid, string> { [actionGenreId] = "Action" };
        var comedyNames = new Dictionary<Guid, string> { [comedyGenreId] = "Comedy" };
        var recommendations = new List<ScoredRecommendation>();
        for (var index = 0; index < 8; index++)
        {
            recommendations.Add(new ScoredRecommendation(
                CreateCandidate(Guid.NewGuid(), [actionGenreId], actionNames, 8m, 500 - index, null),
                0.9m - (index * 0.01m),
                null));
            recommendations.Add(new ScoredRecommendation(
                CreateCandidate(Guid.NewGuid(), [comedyGenreId], comedyNames, 8m, 400 - index, null),
                0.8m - (index * 0.01m),
                null));
        }

        recommendations = recommendations.OrderByDescending(item => item.Score).ToList();
        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(recommendations, DefaultOptions);

        Assert.Equal(4, diversified.Count(item => item.Candidate.GenreIds.Contains(actionGenreId)));
        Assert.Equal(4, diversified.Count(item => item.Candidate.GenreIds.Contains(comedyGenreId)));
        Assert.Equal(recommendations[0].Candidate.Id, diversified[0].Candidate.Id);
    }

    [Fact]
    public void ApplyDiversityHardCapsFranchiseFamilyKeywords()
    {
        var mcuKeyword = Guid.Parse("abababab-abab-abab-abab-abababababab");
        var recommendations = Enumerable.Range(0, 6)
            .Select(index => new ScoredRecommendation(
                CreateCandidate(Guid.NewGuid(), [SciFiGenreId], new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" }, 8m, 1000 - index, 2000 + index)
                    with { FranchiseKeywordIds = [mcuKeyword] },
                1m - (index * 0.01m),
                null))
            .ToList();

        var diversified = PersonalizedRecommendationEngine.ApplyDiversity(recommendations, DefaultOptions);

        Assert.Equal(2, diversified.Count);
        Assert.Equal(recommendations[0].Candidate.Id, diversified[0].Candidate.Id);
        Assert.Equal(recommendations[1].Candidate.Id, diversified[1].Candidate.Id);
    }

    [Fact]
    public void SelectHomeRecommendedSeparatesSamePrimaryGenreWhenADifferentGenreIsEligibleLater()
    {
        var actionGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var comedyGenreId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var pool = new List<RecommendationItem>
        {
            CreateRailItem(1, actionGenreId, 0.90m),
            CreateRailItem(2, actionGenreId, 0.80m),
            CreateRailItem(3, comedyGenreId, 0.70m)
        };

        var rail = PersonalizedRecommendationEngine.SelectHomeRecommended(pool, DefaultOptions, sectionSize: 3);

        Assert.Equal([pool[0].Id, pool[2].Id, pool[1].Id], rail.Select(item => item.Id).ToArray());
        Assert.NotEqual(rail[0].DiversityGenreIds![0], rail[1].DiversityGenreIds![0]);
        Assert.Equal(pool[0].Id, rail[0].Id);
    }

    [Fact]
    public void SelectHomeRecommendedKeepsScoreOrderWhenPrimaryGenresAlreadyDiffer()
    {
        var actionGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var comedyGenreId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var dramaGenreId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var pool = new List<RecommendationItem>
        {
            CreateRailItem(1, actionGenreId, 0.90m),
            CreateRailItem(2, comedyGenreId, 0.80m),
            CreateRailItem(3, dramaGenreId, 0.70m)
        };

        var rail = PersonalizedRecommendationEngine.SelectHomeRecommended(pool, DefaultOptions, sectionSize: 3);

        Assert.Equal(pool.Select(item => item.Id).ToArray(), rail.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void SelectHomeRecommendedBackfillsSectionSizeFromLowerScoredPoolItems()
    {
        const int collectionId = 86311;
        var pool = new List<RecommendationItem>();
        for (var index = 0; index < 6; index++)
        {
            pool.Add(CreateRailItem(
                index + 1,
                Guid.NewGuid(),
                0.95m - (index * 0.01m),
                collectionId));
        }

        for (var index = 0; index < 9; index++)
        {
            pool.Add(CreateRailItem(
                100 + index,
                Guid.NewGuid(),
                0.40m - (index * 0.01m)));
        }

        var rail = PersonalizedRecommendationEngine.SelectHomeRecommended(pool, DefaultOptions, sectionSize: 10);

        Assert.Equal(10, rail.Count);
        Assert.Equal(1, rail.Count(item => item.TmdbCollectionId == collectionId));
        Assert.Contains(rail, item => item.Score < 0.5m);
        Assert.All(rail, item => Assert.Contains(pool, candidate => candidate.Id == item.Id));
        Assert.DoesNotContain(rail, item => item.Id == pool[1].Id);
    }

    [Fact]
    public void SelectHomeRecommendedDoesNotInventItemsOutsideTheScoredPool()
    {
        var pool = Enumerable.Range(0, 4)
            .Select(index => CreateRailItem(index + 1, Guid.NewGuid(), 0.90m - (index * 0.01m)))
            .ToList();
        var outsideId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var rail = PersonalizedRecommendationEngine.SelectHomeRecommended(pool, DefaultOptions, sectionSize: 10);

        Assert.Equal(4, rail.Count);
        Assert.Equal(pool.Select(item => item.Id).ToArray(), rail.Select(item => item.Id).ToArray());
        Assert.DoesNotContain(rail, item => item.Id == outsideId);
    }

    [Fact]
    public void SelectHomeRecommendedStaysShortWhenCapsExhaustTheScoredPool()
    {
        var actionGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var pool = Enumerable.Range(0, 12)
            .Select(index => CreateRailItem(index + 1, actionGenreId, 0.90m - (index * 0.01m)))
            .ToList();

        var rail = PersonalizedRecommendationEngine.SelectHomeRecommended(pool, DefaultOptions, sectionSize: 10);

        Assert.Equal(DefaultOptions.DiversityMaxPerGenre, rail.Count);
        Assert.All(rail, item => Assert.Contains(pool, candidate => candidate.Id == item.Id));
        Assert.Equal(pool.Take(DefaultOptions.DiversityMaxPerGenre).Select(item => item.Id).ToArray(), rail.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void SelectHomeRecommendedKeepsCollectionCapOfOneAndFranchiseCapOfTwo()
    {
        const int collectionId = 42;
        var franchiseKeywordId = Guid.Parse("abababab-abab-abab-abab-abababababab");
        var pool = new List<RecommendationItem>();

        for (var index = 0; index < 3; index++)
        {
            pool.Add(CreateRailItem(
                index + 1,
                Guid.NewGuid(),
                0.99m - (index * 0.01m),
                collectionId));
        }

        for (var index = 0; index < 4; index++)
        {
            pool.Add(CreateRailItem(
                20 + index,
                Guid.NewGuid(),
                0.90m - (index * 0.01m),
                collectionId: 5000 + index,
                franchiseKeywordId: franchiseKeywordId));
        }

        for (var index = 0; index < 10; index++)
        {
            pool.Add(CreateRailItem(
                40 + index,
                Guid.NewGuid(),
                0.50m - (index * 0.01m),
                collectionId: 8000 + index));
        }

        var rail = PersonalizedRecommendationEngine.SelectHomeRecommended(pool, DefaultOptions, sectionSize: 10);

        Assert.Equal(10, rail.Count);
        Assert.Equal(1, rail.Count(item => item.TmdbCollectionId == collectionId));
        Assert.Equal(2, rail.Count(item => item.FranchiseKeywordIds?.Contains(franchiseKeywordId) == true));
        Assert.Equal(1, DefaultOptions.DiversityMaxPerCollection);
        Assert.Equal(2, DefaultOptions.DiversityMaxPerFranchiseFamily);
        Assert.Equal(
            ["marvel cinematic universe", "dc extended universe", "star wars", "james bond"],
            DefaultOptions.DiversityFranchiseKeywordNames);
        Assert.All(rail, item => Assert.Contains(pool, candidate => candidate.Id == item.Id));
    }

    [Fact]
    public void ScoreCandidatesPushesLowRatedGenreBelowLikedGenre()
    {
        var horrorGenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var horrorNames = new Dictionary<Guid, string> { [horrorGenreId] = "Horror" };
        var sciFiNames = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var signals = new List<UserBehaviorSignal>
        {
            CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-2)),
            CreateSignal(
                UserBehaviorSignalTypes.Rating,
                3,
                UtcNow.AddDays(-1),
                genreIds: [horrorGenreId],
                genreNames: horrorNames)
        };
        var preferences = PersonalizedRecommendationEngine.BuildGenrePreferences(signals, DefaultOptions, UtcNow);
        var liked = CreateCandidate(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), [SciFiGenreId], sciFiNames, 7m, 100, null);
        var disliked = CreateCandidate(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), [horrorGenreId], horrorNames, 9m, 5000, null);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [disliked, liked],
            signals,
            preferences,
            new Dictionary<Guid, decimal>(),
            DefaultOptions,
            UtcNow);

        Assert.True(preferences[horrorGenreId].Score < 0m);
        Assert.Equal(liked.Id, scored[0].Candidate.Id);
        Assert.DoesNotContain(horrorGenreId, PersonalizedRecommendationEngine.SelectPositiveGenreIds(preferences));
    }

    [Fact]
    public void ScoreCandidatesReasonUsesBestBehaviorSignalRatherThanFirstWatched()
    {
        var sciFiNames = new Dictionary<Guid, string> { [SciFiGenreId] = "Science Fiction" };
        var weakWatched = CreateSignal(
            UserBehaviorSignalTypes.Watched,
            null,
            UtcNow.AddDays(-1),
            catalogVoteAverage: 2m,
            catalogYear: 1960,
            title: "Weak Match");
        var strongWatched = CreateSignal(
            UserBehaviorSignalTypes.Watched,
            null,
            UtcNow.AddDays(-20),
            catalogVoteAverage: 8m,
            catalogYear: 2014,
            title: "Strong Match");
        var candidate = CreateCandidate(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            [SciFiGenreId],
            sciFiNames,
            8m,
            1000,
            null,
            year: 2014);

        var scored = PersonalizedRecommendationEngine.ScoreCandidates(
            [candidate],
            [weakWatched, strongWatched],
            new Dictionary<Guid, (decimal Score, string Name)>(),
            new Dictionary<Guid, decimal>(),
            BehaviorOnlyOptions(),
            UtcNow);

        Assert.Equal("Because you watched Strong Match", scored[0].Reason);
    }

    private static RecommendationItem CreateRailItem(
        int seed,
        Guid genreId,
        decimal score,
        int? collectionId = null,
        Guid? franchiseKeywordId = null) =>
        new(
            Guid.Parse($"dddddddd-dddd-dddd-dddd-{seed:D012}"),
            "movie",
            $"Title {seed}",
            null,
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            8m,
            100,
            2020,
            score,
            null,
            [genreId],
            collectionId,
            franchiseKeywordId is Guid keywordId ? [keywordId] : null);

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
        IReadOnlyDictionary<Guid, string>? genreNames = null,
        string title = "Interstellar") =>
        new(
            Guid.NewGuid(),
            "movie",
            signalType,
            title,
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
