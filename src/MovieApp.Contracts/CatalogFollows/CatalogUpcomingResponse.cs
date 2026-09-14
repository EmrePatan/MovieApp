namespace MovieApp.Contracts.CatalogFollows;

public sealed record CatalogUpcomingResponse(
    IReadOnlyList<CatalogUpcomingItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
