namespace MovieApp.Contracts.WatchHistory;

public sealed record WatchStatusBatchRequest(
    IReadOnlyList<Guid>? MovieIds,
    IReadOnlyList<Guid>? TvShowIds);
