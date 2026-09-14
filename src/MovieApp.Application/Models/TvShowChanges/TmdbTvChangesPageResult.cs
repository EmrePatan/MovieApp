namespace MovieApp.Application.Models.TvShowChanges;

public sealed record TmdbTvChangesPageResult(
    IReadOnlyList<int> ChangedTmdbIds,
    int Page,
    int TotalPages);
