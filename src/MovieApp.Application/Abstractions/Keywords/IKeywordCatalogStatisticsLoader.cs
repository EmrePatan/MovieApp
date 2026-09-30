namespace MovieApp.Application.Abstractions.Keywords;

public interface IKeywordCatalogStatisticsLoader
{
    Task<KeywordCatalogStatisticsLoadResult?> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record KeywordCatalogStatisticsLoadResult(
    int CatalogDocumentCount,
    IReadOnlyDictionary<Guid, int> DocumentFrequencyByKeywordId);
