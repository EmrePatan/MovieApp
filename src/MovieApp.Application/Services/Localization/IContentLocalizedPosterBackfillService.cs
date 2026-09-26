namespace MovieApp.Application.Services.Localization;

public sealed record ContentLocalizedPosterBackfillRequest(
    int BatchSize = 25,
    int MaxItems = 250,
    int MaxConcurrency = 3,
    int DelayBetweenRequestsMs = 250,
    Guid? StartAfterMovieId = null,
    Guid? StartAfterTvShowId = null);

public sealed record ContentLocalizedPosterBackfillResult(
    int Processed,
    int Succeeded,
    int Failed,
    int ProviderDetailCalls,
    Guid? LastProcessedMovieId,
    Guid? LastProcessedTvShowId);

public interface IContentLocalizedPosterBackfillService
{
    Task<ContentLocalizedPosterBackfillResult> RunAsync(
        ContentLocalizedPosterBackfillRequest request,
        CancellationToken cancellationToken = default);
}
