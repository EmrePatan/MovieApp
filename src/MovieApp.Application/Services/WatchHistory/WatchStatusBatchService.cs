using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.WatchHistory;

namespace MovieApp.Application.Services.WatchHistory;

public sealed class WatchStatusBatchService(
    ICurrentUser currentUser,
    IWatchStatusBatchReadRepository repository) : IWatchStatusBatchService
{
    public const int MaxIdsPerKind = 100;

    public async Task<WatchStatusBatchResult> GetAsync(
        IReadOnlyList<Guid>? movieIds,
        IReadOnlyList<Guid>? tvShowIds,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var movies = Distinct(movieIds);
        var tvShows = Distinct(tvShowIds);

        if (movies.Count > MaxIdsPerKind || tvShows.Count > MaxIdsPerKind)
        {
            throw new ValidationException(
                $"At most {MaxIdsPerKind} movie ids and {MaxIdsPerKind} TV show ids can be requested at once.");
        }

        // Both reads share one scoped DbContext, so they run one after the other.
        var watchedMovies = movies.Count == 0
            ? []
            : await repository.GetWatchedMoviesAsync(userId, movies, cancellationToken);
        var progressRows = tvShows.Count == 0
            ? []
            : await repository.GetTvShowRegularProgressAsync(userId, tvShows, cancellationToken);

        var watchedAtByMovie = watchedMovies
            .GroupBy(row => row.MovieId)
            .ToDictionary(group => group.Key, group => group.Max(row => row.WatchedAt));
        var progressByShow = progressRows
            .GroupBy(row => row.TvShowId)
            .ToDictionary(group => group.Key, group => group.First());

        var movieResults = movies
            .Select(movieId => watchedAtByMovie.TryGetValue(movieId, out var watchedAt)
                ? new MovieWatchStatusResult(movieId, true, watchedAt)
                : new MovieWatchStatusResult(movieId, false, null))
            .ToList();

        var tvResults = tvShows
            .Select(tvShowId =>
            {
                if (!progressByShow.TryGetValue(tvShowId, out var progress))
                {
                    return new TvShowWatchStatusItemResult(tvShowId, false, 0, 0);
                }

                return new TvShowWatchStatusItemResult(
                    tvShowId,
                    TvShowCompletionPolicy.IsCaughtUp(
                        progress.RegularTotalEpisodes,
                        progress.RegularWatchedEpisodes),
                    progress.RegularTotalEpisodes,
                    progress.RegularWatchedEpisodes);
            })
            .ToList();

        return new WatchStatusBatchResult(movieResults, tvResults);
    }

    private static List<Guid> Distinct(IReadOnlyList<Guid>? ids) =>
        ids is null ? [] : ids.Where(id => id != Guid.Empty).Distinct().ToList();
}
