namespace MovieApp.Application.Models.Keywords;

public sealed record CatalogKeywordCoverageSnapshot(
    int MovieEligible,
    int MovieSynced,
    int MovieUnsynced,
    decimal MovieCoveragePercent,
    int TvEligible,
    int TvSynced,
    int TvUnsynced,
    decimal TvCoveragePercent,
    int OverallEligible,
    int OverallSynced,
    int OverallUnsynced,
    decimal OverallCoveragePercent);
