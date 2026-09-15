namespace MovieApp.Application.Models.Changes;

public sealed record TmdbChangesPageResult(
    IReadOnlyList<int> ChangedTmdbIds,
    int Page,
    int TotalPages);
