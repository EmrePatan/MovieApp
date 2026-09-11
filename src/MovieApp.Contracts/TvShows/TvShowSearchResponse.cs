namespace MovieApp.Contracts.TvShows;

public sealed record TvShowSearchResponse(
    IReadOnlyList<TvShowSearchItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
