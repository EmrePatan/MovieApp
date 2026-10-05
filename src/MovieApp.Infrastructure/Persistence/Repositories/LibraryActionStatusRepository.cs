using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Library;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class LibraryActionStatusRepository(ApplicationDbContext dbContext) : ILibraryActionStatusRepository
{
    public async Task<LibraryActionSnapshot> GetMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var row = await LibraryActionStatusSqlQueries.GetMovieStatusAsync(
            dbContext,
            userId,
            movieId,
            cancellationToken);

        var watchlistIds = row.WatchlistIds;

        return new LibraryActionSnapshot(
            "movie",
            movieId,
            row.IsFavorited,
            watchlistIds.Length != 0,
            watchlistIds,
            row.IsFollowing,
            NotifyNewSeasons: false,
            NotifyNewEpisodes: false,
            BaselineEstablished: false,
            IsWatched: row.WatchedAt is not null,
            WatchedAt: row.WatchedAt);
    }

    public async Task<LibraryActionSnapshot> GetTvShowAsync(
        Guid userId,
        Guid tvShowId,
        Guid? episodeId,
        CancellationToken cancellationToken = default)
    {
        var row = await LibraryActionStatusSqlQueries.GetTvShowStatusAsync(
            dbContext,
            userId,
            tvShowId,
            episodeId,
            cancellationToken);

        var watchlistIds = row.WatchlistIds;

        DateTime? watchedAt = null;
        bool? isWatched = null;
        if (episodeId is not null)
        {
            watchedAt = row.WatchedAt;
            isWatched = watchedAt is not null;
        }

        return new LibraryActionSnapshot(
            "tv",
            tvShowId,
            row.IsFavorited,
            watchlistIds.Length != 0,
            watchlistIds,
            row.IsFollowing,
            row.NotifyNewSeasons ?? true,
            row.NotifyNewEpisodes ?? true,
            row.BaselineEstablishedAtUtc is not null,
            isWatched,
            watchedAt);
    }
}
