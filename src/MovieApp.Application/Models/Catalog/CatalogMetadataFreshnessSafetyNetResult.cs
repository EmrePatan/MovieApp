namespace MovieApp.Application.Models.Catalog;

public sealed record CatalogMetadataFreshnessSafetyNetResult(
    int EligibleStaleCount,
    int SelectedBatchCount,
    int MoviesSelected,
    int TvShowsSelected,
    int Refreshed,
    int Skipped,
    int Failed,
    int RemainingStaleEstimate,
    long DurationMs);
