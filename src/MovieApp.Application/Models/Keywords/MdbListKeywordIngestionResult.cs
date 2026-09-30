namespace MovieApp.Application.Models.Keywords;

public enum MdbListKeywordIngestionStatus
{
    Succeeded,
    CatalogNotFound,
    NotEligible,
    TransportUnavailable,
}

public sealed record MdbListKeywordResolutionStats(
    int ProviderKeywordCount,
    int PromotedCanonicalCount,
    int SkippedNoMatchCount,
    int SkippedAmbiguousCount,
    int ExternalReferencesCreated,
    int ExternalReferencesReused);

public sealed record MdbListKeywordIngestionResult(
    MdbListKeywordIngestionStatus Status,
    MdbListKeywordResolutionStats? Stats = null,
    int MdbListSourceCount = 0,
    DateTime? MdbListKeywordsSyncedAtUtc = null);
