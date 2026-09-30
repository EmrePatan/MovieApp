using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;

// KeywordCatalogStatisticsLoadResult lives in Abstractions.Keywords

namespace MovieApp.Application.Recommendations;

public sealed class KeywordCatalogStatisticsSnapshot : IKeywordCatalogStatisticsSnapshot
{
    public static KeywordCatalogStatisticsSnapshot Empty { get; } = new(
        catalogDocumentCount: 0,
        entries: new Dictionary<Guid, KeywordCatalogStatisticsEntry>(),
        refreshedAtUtc: null);

    private readonly Dictionary<Guid, KeywordCatalogStatisticsEntry> _entries;

    private KeywordCatalogStatisticsSnapshot(
        int catalogDocumentCount,
        Dictionary<Guid, KeywordCatalogStatisticsEntry> entries,
        DateTimeOffset? refreshedAtUtc)
    {
        CatalogDocumentCount = catalogDocumentCount;
        _entries = entries;
        RefreshedAtUtc = refreshedAtUtc;
        KeywordCount = entries.Count;
        GenericKeywordCount = entries.Values.Count(entry => entry.AppliesGenericDampening);
    }

    public bool IsAvailable => CatalogDocumentCount > 0 && KeywordCount > 0;

    public int CatalogDocumentCount { get; }

    public int KeywordCount { get; }

    public int GenericKeywordCount { get; }

    public DateTimeOffset? RefreshedAtUtc { get; }

    public bool TryGetStatistics(Guid keywordId, out KeywordCatalogStatisticsEntry entry) =>
        _entries.TryGetValue(keywordId, out entry);

    public static KeywordCatalogStatisticsSnapshot Create(
        KeywordCatalogStatisticsLoadResult loadResult,
        KeywordCatalogStatisticsOptions options,
        DateTimeOffset refreshedAtUtc)
    {
        var catalogDocumentCount = loadResult.CatalogDocumentCount;
        var idfWeights = KeywordCatalogStatisticsMath.BuildIdfWeights(
            loadResult.DocumentFrequencyByKeywordId,
            catalogDocumentCount);

        var entries = new Dictionary<Guid, KeywordCatalogStatisticsEntry>(loadResult.DocumentFrequencyByKeywordId.Count);
        foreach (var (keywordId, documentFrequency) in loadResult.DocumentFrequencyByKeywordId)
        {
            var appliesGeneric = KeywordCatalogStatisticsMath.AppliesGenericDampening(
                documentFrequency,
                catalogDocumentCount,
                options.GenericDocumentFrequencyRatio);

            entries[keywordId] = new KeywordCatalogStatisticsEntry(
                documentFrequency,
                idfWeights.GetValueOrDefault(keywordId, 1d),
                appliesGeneric);
        }

        return new KeywordCatalogStatisticsSnapshot(catalogDocumentCount, entries, refreshedAtUtc);
    }
}
