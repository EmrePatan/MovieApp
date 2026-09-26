namespace MovieApp.Application.Models.Search;

public sealed record ContentSearchTitleProviderEnrichmentResult(
    int Processed,
    int Succeeded,
    int Failed,
    int Skipped,
    int ProviderDetailCalls,
    Guid? LastProcessedMovieId,
    Guid? LastProcessedTvShowId);
