using MovieApp.Application.Models.WatchHistory;

namespace MovieApp.Application.Abstractions.Persistence;

/// <summary>
/// Set-based reads of the current user's watch state for many titles at once, so list screens
/// do not need one request (and several queries) per poster.
/// </summary>
public interface IWatchStatusBatchReadRepository
{
    Task<IReadOnlyList<WatchedMovieStatusRow>> GetWatchedMoviesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Regular-season (season number &gt;= 1) released-episode totals and watched counts per TV show.
    /// Uses the same counting rules as the single-show progress endpoint.
    /// </summary>
    Task<IReadOnlyList<TvShowRegularProgressRow>> GetTvShowRegularProgressAsync(
        Guid userId,
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default);
}
