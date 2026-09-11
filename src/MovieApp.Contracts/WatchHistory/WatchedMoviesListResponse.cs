namespace MovieApp.Contracts.WatchHistory;

public sealed record WatchedMoviesListResponse(
    IReadOnlyList<WatchedMovieResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
