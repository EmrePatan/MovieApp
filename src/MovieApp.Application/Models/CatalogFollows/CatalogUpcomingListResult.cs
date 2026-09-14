namespace MovieApp.Application.Models.CatalogFollows;

public sealed record CatalogUpcomingListResult(
    IReadOnlyList<CatalogUpcomingItemResult> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
