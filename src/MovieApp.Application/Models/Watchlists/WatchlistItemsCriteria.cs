using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistItemsCriteria(
    Guid WatchlistId,
    SearchContentType MediaType,
    WatchlistItemsSort Sort,
    int Page,
    int PageSize);
