namespace MovieApp.Contracts.CatalogFollows;

public sealed record CatalogFollowsResponse(
    IReadOnlyList<CatalogFollowItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
