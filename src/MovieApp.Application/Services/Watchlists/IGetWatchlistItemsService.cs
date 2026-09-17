using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IGetWatchlistItemsService
{
    Task<WatchlistItemsResult> GetAsync(
        Guid watchlistId,
        SearchContentType mediaType,
        WatchlistItemsSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
