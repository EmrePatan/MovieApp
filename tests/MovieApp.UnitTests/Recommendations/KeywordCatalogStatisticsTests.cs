using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Infrastructure.Keywords;

namespace MovieApp.UnitTests.Recommendations;

public sealed class KeywordCatalogStatisticsTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly RecommendationOptions DefaultOptions = new();
    private static readonly Guid KeywordRare = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid KeywordGeneric = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid KeywordMissing = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void BuildIdfWeights_MatchesBenchmarkFormula()
    {
        var keywordId = Guid.NewGuid();
        var documentFrequency = new Dictionary<Guid, int> { [keywordId] = 42 };
        const int catalogCount = 10_000;

        var production = KeywordCatalogStatisticsMath.BuildIdfWeights(documentFrequency, catalogCount);
        var expected = Math.Log((catalogCount + 1d) / (42 + 1d)) + 1d;

        Assert.Equal(expected, production[keywordId], precision: 12);
    }

    [Fact]
    public void GenericDampening_AppliesAtExactlyTenPercentRatio()
    {
        Assert.True(KeywordCatalogStatisticsMath.AppliesGenericDampening(10, 100, 0.10));
    }

    [Fact]
    public void GenericDampening_DoesNotApplyBelowThreshold()
    {
        Assert.False(KeywordCatalogStatisticsMath.AppliesGenericDampening(9, 100, 0.10));
    }

    [Fact]
    public void GenericDampening_AppliesAboveThreshold()
    {
        Assert.True(KeywordCatalogStatisticsMath.AppliesGenericDampening(11, 100, 0.10));
    }

    [Fact]
    public void RareKeywordReceivesHigherPreferenceWeightThanGenericKeyword()
    {
        var snapshot = CreateSnapshot(
            catalogCount: 100,
            (KeywordRare, 1),
            (KeywordGeneric, 15));

        var rareMultiplier = KeywordCatalogStatisticsMath.GetPreferenceWeightMultiplier(snapshot, KeywordRare, 0.5);
        var genericMultiplier = KeywordCatalogStatisticsMath.GetPreferenceWeightMultiplier(snapshot, KeywordGeneric, 0.5);

        Assert.True(rareMultiplier > genericMultiplier);
    }

    [Fact]
    public void MissingKeywordEntryUsesNeutralMultiplier()
    {
        var snapshot = CreateSnapshot(catalogCount: 100, (KeywordRare, 1));

        var multiplier = KeywordCatalogStatisticsMath.GetPreferenceWeightMultiplier(snapshot, KeywordMissing, 0.5);

        Assert.Equal(1d, multiplier);
    }

    [Fact]
    public void EmptySnapshotIsNotAvailableAndFallsBackToLegacyPreferences()
    {
        var signals = CreateFavoriteSignal(KeywordRare);
        var legacy = KeywordAffinityScorer.BuildKeywordPreferences(signals, DefaultOptions, UtcNow);
        var withEmpty = KeywordAffinityScorer.BuildKeywordPreferences(
            signals,
            DefaultOptions,
            UtcNow,
            KeywordCatalogStatisticsSnapshot.Empty,
            EnabledStatisticsOptions());

        Assert.Equal(legacy[KeywordRare], withEmpty[KeywordRare]);
    }

    [Fact]
    public void EnabledStatisticsWithUnavailableSnapshotMatchesLegacyBehavior()
    {
        var signals = CreateFavoriteSignal(KeywordRare);
        var legacy = KeywordAffinityScorer.BuildKeywordPreferences(signals, DefaultOptions, UtcNow);
        var builder = new KeywordAffinityPreferenceBuilder(
            new StubKeywordCatalogStatisticsProvider(KeywordCatalogStatisticsSnapshot.Empty),
            Options.Create(new KeywordCatalogStatisticsOptions { Enabled = true }));

        var built = builder.Build(signals, DefaultOptions, UtcNow);

        Assert.Equal(legacy[KeywordRare], built[KeywordRare]);
    }

    [Fact]
    public void ProviderPublishesSnapshotAtomically()
    {
        var provider = new KeywordCatalogStatisticsProvider();
        var first = CreateSnapshot(catalogCount: 50, (KeywordRare, 2));
        var second = CreateSnapshot(catalogCount: 60, (KeywordGeneric, 30));

        provider.Publish(first);
        Assert.Equal(50, provider.Current.CatalogDocumentCount);

        provider.Publish(second);
        Assert.Equal(60, provider.Current.CatalogDocumentCount);
        Assert.True(provider.Current.TryGetStatistics(KeywordGeneric, out _));
    }

    [Fact]
    public async Task ConcurrentReadsSeeConsistentSnapshot()
    {
        var provider = new KeywordCatalogStatisticsProvider();
        provider.Publish(CreateSnapshot(catalogCount: 100, (KeywordRare, 5)));

        var exceptions = new List<Exception>();
        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 500; i++)
                {
                    var snapshot = provider.Current;
                    if (!snapshot.IsAvailable || snapshot.CatalogDocumentCount != 100)
                    {
                        throw new InvalidOperationException("Inconsistent snapshot read.");
                    }
                }
            }
            catch (Exception exception)
            {
                lock (exceptions)
                {
                    exceptions.Add(exception);
                }
            }
        }));

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    [Fact]
    public void FrequencyAwarePreferencesShiftRelativeWeightsWithoutSecondIdfPass()
    {
        var signals = new List<UserBehaviorSignal>
        {
            CreateFavoriteSignal(KeywordRare)[0],
            CreateFavoriteSignal(KeywordGeneric, Guid.NewGuid())[0]
        };

        var legacyPreferences = KeywordAffinityScorer.BuildKeywordPreferences(signals, DefaultOptions, UtcNow);
        var snapshot = CreateSnapshot(catalogCount: 100, (KeywordRare, 1), (KeywordGeneric, 20));
        var frequencyAwarePreferences = KeywordAffinityScorer.BuildKeywordPreferences(
            signals,
            DefaultOptions,
            UtcNow,
            snapshot,
            EnabledStatisticsOptions());

        Assert.True(frequencyAwarePreferences[KeywordRare] > frequencyAwarePreferences[KeywordGeneric]);
        Assert.Equal(legacyPreferences[KeywordRare], legacyPreferences[KeywordGeneric]);
    }

    [Fact]
    public void CalculateKeywordScoreDependsOnlyOnPreferenceDictionary()
    {
        var preferences = new Dictionary<Guid, decimal> { [KeywordRare] = 0.42m };
        var candidate = CreateCandidate([KeywordRare]);

        var first = KeywordAffinityScorer.CalculateKeywordScore(candidate, preferences);
        var second = KeywordAffinityScorer.CalculateKeywordScore(candidate, preferences);

        Assert.Equal(first, second);
    }

    [Fact]
    public void PersonalizedKeywordWeightRemainsDefaultFifteenPercent()
    {
        Assert.Equal(0.15, new RecommendationOptions().PersonalizedKeywordWeight);
    }

    [Fact]
    public async Task RefreshService_KeepsPreviousSnapshotWhenLoadFails()
    {
        var provider = new KeywordCatalogStatisticsProvider();
        var valid = CreateSnapshot(catalogCount: 100, (KeywordRare, 3));
        provider.Publish(valid);

        var service = new KeywordCatalogStatisticsRefreshService(
            new FailingKeywordCatalogStatisticsLoader(),
            provider,
            Options.Create(EnabledStatisticsOptions()),
            NullLogger<KeywordCatalogStatisticsRefreshService>.Instance);

        var result = await service.RefreshAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(100, provider.Current.CatalogDocumentCount);
    }

    [Fact]
    public async Task RefreshService_PublishesLoadedSnapshot()
    {
        var provider = new KeywordCatalogStatisticsProvider();
        var loadResult = new KeywordCatalogStatisticsLoadResult(
            200,
            new Dictionary<Guid, int> { [KeywordRare] = 4, [KeywordGeneric] = 40 });

        var service = new KeywordCatalogStatisticsRefreshService(
            new StubKeywordCatalogStatisticsLoader(loadResult),
            provider,
            Options.Create(EnabledStatisticsOptions()),
            NullLogger<KeywordCatalogStatisticsRefreshService>.Instance);

        var result = await service.RefreshAsync();

        Assert.True(result.Succeeded);
        Assert.True(result.SnapshotPublished);
        Assert.Equal(200, provider.Current.CatalogDocumentCount);
        Assert.True(provider.Current.TryGetStatistics(KeywordGeneric, out var entry));
        Assert.True(entry.AppliesGenericDampening);
    }

    private static KeywordCatalogStatisticsOptions EnabledStatisticsOptions() =>
        new()
        {
            Enabled = true,
            GenericDocumentFrequencyRatio = 0.10,
            GenericDampeningFactor = 0.5
        };

    private static KeywordCatalogStatisticsSnapshot CreateSnapshot(
        int catalogCount,
        params (Guid KeywordId, int DocumentFrequency)[] entries)
    {
        var loadResult = new KeywordCatalogStatisticsLoadResult(
            catalogCount,
            entries.ToDictionary(entry => entry.KeywordId, entry => entry.DocumentFrequency));

        return KeywordCatalogStatisticsSnapshot.Create(
            loadResult,
            EnabledStatisticsOptions(),
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<UserBehaviorSignal> CreateFavoriteSignal(
        Guid keywordId,
        Guid? contentId = null) =>
    [
        new(
            contentId ?? Guid.NewGuid(),
            "movie",
            UserBehaviorSignalTypes.Favorite,
            "Title",
            null,
            UtcNow,
            [],
            new Dictionary<Guid, string>(),
            [])
        {
            KeywordIds = [keywordId]
        }
    ];

    private static PersonalizedCandidateProfile CreateCandidate(IReadOnlyList<Guid> keywordIds) =>
        new(
            Guid.NewGuid(),
            "movie",
            "Candidate",
            null,
            null,
            null,
            null,
            new DateOnly(2014, 1, 1),
            8m,
            100,
            2014,
            [],
            new Dictionary<Guid, string>(),
            [],
            null)
        {
            KeywordIds = keywordIds
        };

    private sealed class StubKeywordCatalogStatisticsProvider(IKeywordCatalogStatisticsSnapshot current)
        : IKeywordCatalogStatisticsProvider
    {
        public IKeywordCatalogStatisticsSnapshot Current { get; } = current;

        public bool IsFrequencyAwareActive => Current.IsAvailable;
    }

    private sealed class StubKeywordCatalogStatisticsLoader(KeywordCatalogStatisticsLoadResult result)
        : IKeywordCatalogStatisticsLoader
    {
        public Task<KeywordCatalogStatisticsLoadResult?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<KeywordCatalogStatisticsLoadResult?>(result);
    }

    private sealed class FailingKeywordCatalogStatisticsLoader : IKeywordCatalogStatisticsLoader
    {
        public Task<KeywordCatalogStatisticsLoadResult?> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated load failure");
    }
}
