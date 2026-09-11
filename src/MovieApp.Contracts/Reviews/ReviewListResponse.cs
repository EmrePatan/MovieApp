namespace MovieApp.Contracts.Reviews;

public sealed record ReviewListResponse(
    IReadOnlyList<ReviewResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
