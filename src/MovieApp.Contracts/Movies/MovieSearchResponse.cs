namespace MovieApp.Contracts.Movies;

public sealed record MovieSearchResponse(
    IReadOnlyList<MovieSearchItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
