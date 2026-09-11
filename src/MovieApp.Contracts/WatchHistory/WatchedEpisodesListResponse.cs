namespace MovieApp.Contracts.WatchHistory;

public sealed record WatchedEpisodesListResponse(
    IReadOnlyList<WatchedEpisodeResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
