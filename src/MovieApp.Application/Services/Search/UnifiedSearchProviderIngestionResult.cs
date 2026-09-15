using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public sealed record UnifiedSearchProviderIngestionResult(
    bool MovieRefreshRequired,
    bool TvRefreshRequired,
    bool PersonRefreshRequired,
    bool MovieRefreshAttempted,
    bool TvRefreshAttempted,
    bool PersonRefreshAttempted,
    bool MovieRefreshSucceeded,
    bool TvRefreshSucceeded,
    bool PersonRefreshSucceeded,
    PaginatedResult<SearchItem>? Result = null)
{
    public bool IsFullySuccessful =>
        (!MovieRefreshRequired || MovieRefreshSucceeded) &&
        (!TvRefreshRequired || TvRefreshSucceeded) &&
        (!PersonRefreshRequired || PersonRefreshSucceeded);

    public bool HasPartialFailure =>
        (MovieRefreshAttempted && !MovieRefreshSucceeded) ||
        (TvRefreshAttempted && !TvRefreshSucceeded) ||
        (PersonRefreshAttempted && !PersonRefreshSucceeded);

    public static UnifiedSearchProviderIngestionResult NotRequired() =>
        new(false, false, false, false, false, false, false, false, false);
}
