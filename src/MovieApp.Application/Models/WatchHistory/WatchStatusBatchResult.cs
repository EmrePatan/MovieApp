namespace MovieApp.Application.Models.WatchHistory;

public sealed record WatchedMovieStatusRow(Guid MovieId, DateTime WatchedAt);

public sealed record TvShowRegularProgressRow(
    Guid TvShowId,
    int RegularTotalEpisodes,
    int RegularWatchedEpisodes);

public sealed record TvShowWatchStatusItemResult(
    Guid TvShowId,
    bool IsFullyWatched,
    int RegularTotalEpisodes,
    int RegularWatchedEpisodes);

public sealed record WatchStatusBatchResult(
    IReadOnlyList<MovieWatchStatusResult> Movies,
    IReadOnlyList<TvShowWatchStatusItemResult> TvShows);
