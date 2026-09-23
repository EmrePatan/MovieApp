using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Discovery;

public interface IWorldCinemaService
{
    Task<PaginatedResult<SearchItem>> GetWorldCinemaAsync(
        WorldCinemaCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
