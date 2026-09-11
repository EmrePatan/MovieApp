namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistItemsResult(
    IReadOnlyList<WatchlistItemMovieResult> Movies,
    IReadOnlyList<WatchlistItemTvShowResult> TvShows,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
