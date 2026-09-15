namespace MovieApp.Application.Models.Providers;

public sealed record PersonProviderSearchResult(
    IReadOnlyList<PersonProviderSummary> Results,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
