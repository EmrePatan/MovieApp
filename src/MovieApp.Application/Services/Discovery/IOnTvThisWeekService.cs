using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Discovery;

public interface IOnTvThisWeekService
{
    Task<PaginatedResult<SearchItem>> GetOnTvThisWeekAsync(
        OnTvThisWeekCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
