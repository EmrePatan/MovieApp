namespace MovieApp.Application.Abstractions.Keywords;

public interface IKeywordCatalogStatisticsSnapshot
{
    bool IsAvailable { get; }

    int CatalogDocumentCount { get; }

    int KeywordCount { get; }

    int GenericKeywordCount { get; }

    DateTimeOffset? RefreshedAtUtc { get; }

    bool TryGetStatistics(Guid keywordId, out KeywordCatalogStatisticsEntry entry);
}

public readonly record struct KeywordCatalogStatisticsEntry(
    int DocumentFrequency,
    double InverseDocumentFrequency,
    bool AppliesGenericDampening);
