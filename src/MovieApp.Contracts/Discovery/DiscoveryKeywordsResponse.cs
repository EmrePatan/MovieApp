namespace MovieApp.Contracts.Discovery;

public sealed record DiscoveryKeywordsResponse(
    IReadOnlyList<DiscoveryKeywordResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
