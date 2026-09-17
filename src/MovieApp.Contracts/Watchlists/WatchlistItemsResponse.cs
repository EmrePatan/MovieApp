namespace MovieApp.Contracts.Watchlists;

public sealed record WatchlistItemsResponse(
    IReadOnlyList<WatchlistCatalogItemResponse> Items,
    IReadOnlyList<WatchlistMovieItemResponse> Movies,
    IReadOnlyList<WatchlistTvShowItemResponse> TvShows,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
