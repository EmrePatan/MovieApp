using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class WatchDateRepository(ApplicationDbContext dbContext) : IWatchDateRepository
{
    public async Task<IReadOnlyList<WatchedMovie>> GetWatchedMoviesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default) =>
        await dbContext.WatchedMovies
            .Include(watched => watched.Movie)
            .Where(watched => watched.UserId == userId && movieIds.Contains(watched.MovieId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WatchedEpisode>> GetWatchedEpisodesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> episodeIds,
        CancellationToken cancellationToken = default) =>
        await dbContext.WatchedEpisodes
            .Include(watched => watched.Episode)
            .Where(watched => watched.UserId == userId && episodeIds.Contains(watched.EpisodeId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WatchedEpisode>> GetWatchedEpisodesOfShowAsync(
        Guid userId,
        Guid tvShowId,
        bool? undated,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WatchedEpisodes
            .Include(watched => watched.Episode)
            .Where(watched => watched.UserId == userId && watched.Episode.Season.TvShowId == tvShowId);

        if (undated is { } onlyUndated)
        {
            query = query.Where(watched =>
                (watched.WatchedAtPrecision == WatchDatePrecision.Unknown) == onlyUndated);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
