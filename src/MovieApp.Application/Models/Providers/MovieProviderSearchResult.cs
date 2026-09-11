namespace MovieApp.Application.Models.Providers;

public sealed record MovieProviderSearchResult(
    IReadOnlyList<MovieProviderSummary> Results,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
