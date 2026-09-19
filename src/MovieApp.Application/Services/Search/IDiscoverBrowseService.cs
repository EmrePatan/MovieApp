using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IDiscoverBrowseService
{
    Task<PaginatedResult<SearchItem>> BrowseAsync(
        DiscoverBrowseCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
