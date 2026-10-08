using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Keywords;

public interface IKeywordDiscoverSearchService
{
    Task<PaginatedResult<KeywordDiscoverItem>> SearchAsync(
        string query,
        string contentLocale,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
