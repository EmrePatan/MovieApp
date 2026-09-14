namespace MovieApp.Application.Models.CatalogFollows;

public sealed record CatalogFollowsListResult(
    IReadOnlyList<CatalogFollowItemResult> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
