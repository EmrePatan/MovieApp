using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Library;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class LibraryActionStatusRepository(ApplicationDbContext dbContext) : ILibraryActionStatusRepository
{
    public async Task<LibraryActionSnapshot> GetMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var isFavorited = await dbContext.Favorites
            .AsNoTracking()
            .AnyAsync(
                favorite => favorite.UserId == userId && favorite.MovieId == movieId,
                cancellationToken);

        var watchlistIds = await UserWatchlistIdsAsync(
            userId,
            item => item.MovieId == movieId,
            cancellationToken);

        var follow = await dbContext.CatalogFollows
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.UserId == userId &&
                    item.ContentType == CatalogContentType.Movie &&
                    item.ContentId == movieId,
                cancellationToken);

        var watchedAt = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.MovieId == movieId)
            .Select(item => (DateTime?)item.WatchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new LibraryActionSnapshot(
            "movie",
            movieId,
            isFavorited,
            watchlistIds.Count > 0,
            watchlistIds,
            follow,
            NotifyNewSeasons: false,
            NotifyNewEpisodes: false,
            BaselineEstablished: false,
            IsWatched: watchedAt is not null,
            WatchedAt: watchedAt);
    }

    public async Task<LibraryActionSnapshot> GetTvShowAsync(
        Guid userId,
        Guid tvShowId,
        Guid? episodeId,
        CancellationToken cancellationToken = default)
    {
        var isFavorited = await dbContext.Favorites
            .AsNoTracking()
            .AnyAsync(
                favorite => favorite.UserId == userId && favorite.TvShowId == tvShowId,
                cancellationToken);

        var watchlistIds = await UserWatchlistIdsAsync(
            userId,
            item => item.TvShowId == tvShowId,
            cancellationToken);

        var follow = await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId &&
                item.ContentType == CatalogContentType.Tv &&
                item.ContentId == tvShowId)
            .Select(item => new
            {
                item.NotifyNewSeasons,
                item.NotifyNewEpisodes,
                item.BaselineEstablishedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        DateTime? watchedAt = null;
        bool? isWatched = null;
        if (episodeId is Guid episode)
        {
            watchedAt = await dbContext.WatchedEpisodes
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.EpisodeId == episode)
                .Select(item => (DateTime?)item.WatchedAt)
                .FirstOrDefaultAsync(cancellationToken);
            isWatched = watchedAt is not null;
        }

        return new LibraryActionSnapshot(
            "tv",
            tvShowId,
            isFavorited,
            watchlistIds.Count > 0,
            watchlistIds,
            follow is not null,
            follow?.NotifyNewSeasons ?? true,
            follow?.NotifyNewEpisodes ?? true,
            follow?.BaselineEstablishedAtUtc is not null,
            isWatched,
            watchedAt);
    }

    private async Task<IReadOnlyList<Guid>> UserWatchlistIdsAsync(
        Guid userId,
        Expression<Func<WatchlistItem, bool>> itemFilter,
        CancellationToken cancellationToken)
    {
        return await dbContext.Watchlists
            .AsNoTracking()
            .Where(watchlist => watchlist.UserId == userId)
            .SelectMany(watchlist => watchlist.Items)
            .Where(itemFilter)
            .Select(item => item.WatchlistId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
