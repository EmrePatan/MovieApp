using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class AiTasteProfileDataSource(ApplicationDbContext dbContext) : IAiTasteProfileDataSource
{
    public async Task<AiTasteProfileRawData> LoadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
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

        var favorites = await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId && favorite.MovieId != null)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Select(favorite => new AiMovieTitleRow(
                favorite.Movie!.Title,
                favorite.Movie!.ReleaseDate.HasValue ? favorite.Movie.ReleaseDate.Value.Year : null,
                favorite.Movie!.MovieGenres.Select(mg => mg.Genre.Name).ToList()))
            .ToListAsync(cancellationToken);

        var watchlistMovies = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId && item.MovieId != null)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new AiMovieTitleRow(
                item.Movie!.Title,
                item.Movie!.ReleaseDate.HasValue ? item.Movie.ReleaseDate.Value.Year : null,
                item.Movie!.MovieGenres.Select(mg => mg.Genre.Name).ToList()))
            .ToListAsync(cancellationToken);

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

        var watchedTvGenres = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .SelectMany(
                item => item.Episode.Season.TvShow.TvShowGenres,
                (item, tvShowGenre) => tvShowGenre.Genre.Name)
            .GroupBy(genre => genre)
            .Select(group => new AiTvGenreRow(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

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
}
