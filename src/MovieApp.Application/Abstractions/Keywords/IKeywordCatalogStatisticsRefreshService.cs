namespace MovieApp.Application.Abstractions.Keywords;

public interface IKeywordCatalogStatisticsRefreshService
{
    Task<KeywordCatalogStatisticsRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed record KeywordCatalogStatisticsRefreshResult(
    bool Succeeded,
    bool SnapshotPublished,
    int CatalogDocumentCount,
    int KeywordCount,
    int GenericKeywordCount,
    long DurationMilliseconds,
    string? FailureReason);
