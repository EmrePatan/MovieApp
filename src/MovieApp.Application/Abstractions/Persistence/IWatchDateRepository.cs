using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IWatchDateRepository
{
    /// <summary>Tracked rows of this user only, with <c>Movie</c> loaded.</summary>
    Task<IReadOnlyList<WatchedMovie>> GetWatchedMoviesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default);

    /// <summary>Tracked rows of this user only, with <c>Episode</c> loaded.</summary>
    Task<IReadOnlyList<WatchedEpisode>> GetWatchedEpisodesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> episodeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Every watched episode of one show, tracked, with <c>Episode</c> loaded.</summary>
    /// <param name="undated">Null for all, true for "date unknown" rows, false for the rest.</param>
    Task<IReadOnlyList<WatchedEpisode>> GetWatchedEpisodesOfShowAsync(
        Guid userId,
        Guid tvShowId,
        bool? undated,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
