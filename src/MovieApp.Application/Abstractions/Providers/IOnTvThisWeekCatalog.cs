using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface IOnTvThisWeekCatalog
{
    Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
        int page,
        CancellationToken cancellationToken = default);
}
