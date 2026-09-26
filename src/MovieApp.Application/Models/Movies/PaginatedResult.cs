namespace MovieApp.Application.Models.Movies;

public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    string? NextCursor = null)
{
    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
