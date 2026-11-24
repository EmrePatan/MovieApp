namespace MovieApp.Contracts.WatchHistory;

public sealed record TvShowWatchStatusBatchItemResponse(
    Guid TvShowId,
    bool IsFullyWatched,
    int RegularTotalEpisodes,
    int RegularWatchedEpisodes);

public sealed record WatchStatusBatchResponse(
    IReadOnlyList<MovieWatchStatusResponse> Movies,
    IReadOnlyList<TvShowWatchStatusBatchItemResponse> TvShows);
