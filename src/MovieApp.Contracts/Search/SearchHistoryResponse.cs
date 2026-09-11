namespace MovieApp.Contracts.Search;

public sealed record SearchHistoryResponse(
    IReadOnlyList<SearchHistoryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
