using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IWatchedMovieRepository
{
    Task<WatchedMovie?> GetByUserAndMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<(WatchedMovie Entity, bool Created)> UpsertAsync(
        WatchedMovie watchedMovie,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WatchedMovie> Items, int TotalCount)> GetUserWatchedMoviesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Guid MovieId, string Title, DateTime WatchedAt)>> GetRecentForUserAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
