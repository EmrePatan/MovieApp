using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Discovery;

public interface INowInTheatersService
{
    Task<PaginatedResult<SearchItem>> GetNowInTheatersAsync(
        NowInTheatersCriteria criteria,
        CancellationToken cancellationToken = default);
}
