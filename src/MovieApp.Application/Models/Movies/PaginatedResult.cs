namespace MovieApp.Application.Models.Movies;

public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    string? NextCursor = null,
    bool? HasNextPageOverride = null)
{
    public bool HasNextPage => HasNextPageOverride ?? Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
