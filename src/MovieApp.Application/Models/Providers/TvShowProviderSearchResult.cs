namespace MovieApp.Application.Models.Providers;

public sealed record TvShowProviderSearchResult(
    IReadOnlyList<TvShowProviderSummary> Results,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
