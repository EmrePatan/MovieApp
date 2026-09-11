using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface ISearchHistoryService
{
    Task<PaginatedResult<SearchHistoryItem>> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task ClearHistoryAsync(CancellationToken cancellationToken = default);

    Task DeleteHistoryItemAsync(Guid historyId, CancellationToken cancellationToken = default);
}
