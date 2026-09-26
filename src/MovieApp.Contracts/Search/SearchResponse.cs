namespace MovieApp.Contracts.Search;

public sealed record SearchResponse(
    IReadOnlyList<SearchItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage,
    string? NextCursor = null);
