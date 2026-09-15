namespace MovieApp.Contracts.Library;

public sealed record LibraryListResponse(
    IReadOnlyList<LibraryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
