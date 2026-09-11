using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface ISearchService
{
    Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
