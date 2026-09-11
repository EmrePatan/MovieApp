namespace MovieApp.Contracts.WatchHistory;

public sealed record RecentWatchHistoryResponse(
    IReadOnlyList<RecentWatchHistoryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
