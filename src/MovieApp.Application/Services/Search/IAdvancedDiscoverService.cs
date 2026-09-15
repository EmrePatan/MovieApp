using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IAdvancedDiscoverService
{
    Task<PaginatedResult<SearchItem>> DiscoverAsync(
        AdvancedDiscoverCriteria criteria,
        CancellationToken cancellationToken = default);
}
