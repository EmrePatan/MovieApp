namespace MovieApp.Contracts.Reviews;

public sealed record UserReviewListResponse(
    IReadOnlyList<UserReviewListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
