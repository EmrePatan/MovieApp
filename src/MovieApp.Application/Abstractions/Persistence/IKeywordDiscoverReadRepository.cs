using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IKeywordDiscoverReadRepository
{
    Task<PaginatedResult<KeywordDiscoverItem>> SearchAsync(
        string query,
        string contentLocale,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> ResolveTmdbKeywordIdsAsync(
        IReadOnlyList<Guid> keywordIds,
        CancellationToken cancellationToken = default);
}
