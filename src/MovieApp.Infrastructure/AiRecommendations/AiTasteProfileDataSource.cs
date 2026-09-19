using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class AiTasteProfileDataSource(
    ApplicationDbContext dbContext,
    IAiRecommendationPerfContext perfContext) : IAiTasteProfileDataSource
{
    public async Task<AiTasteProfileRawData> LoadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var ratingsStopwatch = Stopwatch.StartNew();
        var ratings = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId && rating.MovieId != null)
            .Select(rating => new AiRatingRow(
                rating.MovieId,
                rating.Movie!.Title,
                rating.Movie!.ReleaseDate.HasValue ? rating.Movie.ReleaseDate.Value.Year : null,
                rating.Movie!.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
                rating.Score))
            .ToListAsync(cancellationToken);
        ratingsStopwatch.Stop();
        perfContext.RecordTasteProfileQuery("Ratings", ratingsStopwatch.ElapsedMilliseconds);

        var favoritesStopwatch = Stopwatch.StartNew();
        var favorites = await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId && favorite.MovieId != null)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Select(favorite => new AiMovieTitleRow(
                favorite.Movie!.Title,
                favorite.Movie!.ReleaseDate.HasValue ? favorite.Movie.ReleaseDate.Value.Year : null,
                favorite.Movie!.MovieGenres.Select(mg => mg.Genre.Name).ToList()))
            .ToListAsync(cancellationToken);
        favoritesStopwatch.Stop();
        perfContext.RecordTasteProfileQuery("Favorites", favoritesStopwatch.ElapsedMilliseconds);

        var watchlistStopwatch = Stopwatch.StartNew();
        var watchlistMovies = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId && item.MovieId != null)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new AiMovieTitleRow(
                item.Movie!.Title,
                item.Movie!.ReleaseDate.HasValue ? item.Movie.ReleaseDate.Value.Year : null,
                item.Movie!.MovieGenres.Select(mg => mg.Genre.Name).ToList()))
            .ToListAsync(cancellationToken);
        watchlistStopwatch.Stop();
        perfContext.RecordTasteProfileQuery("Watchlist", watchlistStopwatch.ElapsedMilliseconds);

        var watchedMoviesStopwatch = Stopwatch.StartNew();
        var watchedMovies = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.WatchedAt)
            .Select(item => new AiWatchedMovieRow(
                item.MovieId,
                item.Movie!.Title,
                item.Movie!.ReleaseDate.HasValue ? item.Movie.ReleaseDate.Value.Year : null,
                item.Movie!.MovieGenres.Select(mg => mg.Genre.Name).ToList()))
            .ToListAsync(cancellationToken);
        watchedMoviesStopwatch.Stop();
        perfContext.RecordTasteProfileQuery("WatchedMovies", watchedMoviesStopwatch.ElapsedMilliseconds);

        var watchedTvGenresStopwatch = Stopwatch.StartNew();
        var watchedTvGenres = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .SelectMany(
                item => item.Episode.Season.TvShow.TvShowGenres,
                (item, tvShowGenre) => tvShowGenre.Genre.Name)
            .GroupBy(genre => genre)
            .Select(group => new AiTvGenreRow(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        watchedTvGenresStopwatch.Stop();
        perfContext.RecordTasteProfileQuery("WatchedTvGenres", watchedTvGenresStopwatch.ElapsedMilliseconds);

        return new AiTasteProfileRawData(
            ratings,
            favorites,
            watchlistMovies,
            watchedMovies,
            watchedTvGenres);
    }

    public async Task<IReadOnlySet<Guid>> GetWatchedMovieIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ids = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.MovieId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetWatchedTvShowIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ids = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Episode.Season.TvShowId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
