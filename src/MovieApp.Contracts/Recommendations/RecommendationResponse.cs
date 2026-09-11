namespace MovieApp.Contracts.Recommendations;

public sealed record RecommendationResponse(
    IReadOnlyList<RecommendationItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
