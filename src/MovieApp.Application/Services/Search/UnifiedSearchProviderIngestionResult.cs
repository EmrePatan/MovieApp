using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public sealed record UnifiedSearchProviderIngestionResult(
    bool MovieRefreshRequired,
    bool TvRefreshRequired,
    bool MovieRefreshAttempted,
    bool TvRefreshAttempted,
    bool MovieRefreshSucceeded,
    bool TvRefreshSucceeded)
{
    public bool IsFullySuccessful =>
        (!MovieRefreshRequired || MovieRefreshSucceeded) &&
        (!TvRefreshRequired || TvRefreshSucceeded);

    public bool HasPartialFailure =>
        (MovieRefreshAttempted && !MovieRefreshSucceeded) ||
        (TvRefreshAttempted && !TvRefreshSucceeded);

    public static UnifiedSearchProviderIngestionResult NotRequired() =>
        new(false, false, false, false, false, false);
}
