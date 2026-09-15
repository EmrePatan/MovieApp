using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogChangesRelevanceRepository(ApplicationDbContext dbContext)
    : ICatalogChangesRelevanceRepository
{
    public async Task<IReadOnlyDictionary<int, Guid>> GetRelevantMovieIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default)
    {
        var relevantMovieIds = BuildRelevantMovieIdsQuery();

        var rows = await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.TmdbId.HasValue && relevantMovieIds.Contains(movie.Id))
            .Select(movie => new { movie.TmdbId, movie.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.TmdbId!.Value, row => row.Id);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetRelevantTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default)
    {
        var relevantTvShowIds = BuildRelevantTvShowIdsQuery();

        var rows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId.HasValue && relevantTvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.TmdbId!.Value, row => row.Id);
    }

    private IQueryable<Guid> BuildRelevantMovieIdsQuery() =>
        dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.ContentType == CatalogContentType.Movie)
            .Select(follow => follow.ContentId)
            .Union(dbContext.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.MovieId != null)
                .Select(favorite => favorite.MovieId!.Value))
            .Union(dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => item.MovieId != null)
                .Select(item => item.MovieId!.Value))
            .Union(dbContext.WatchedMovies
                .AsNoTracking()
                .Select(watchedMovie => watchedMovie.MovieId));

    private IQueryable<Guid> BuildRelevantTvShowIdsQuery()
    {
        var showsWithUnwatchedRegularEpisodes = dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episode.Season.SeasonNumber >= 1)
            .Where(episode => !dbContext.WatchedEpisodes.Any(
                watchedEpisode => watchedEpisode.EpisodeId == episode.Id))
            .Select(episode => episode.Season.TvShowId)
            .Distinct();

        var watchingTvShowIds = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.Episode.Season.SeasonNumber >= 1)
            .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Distinct()
            .Where(tvShowId => showsWithUnwatchedRegularEpisodes.Contains(tvShowId));

        return dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.ContentType == CatalogContentType.Tv)
            .Select(follow => follow.ContentId)
            .Union(dbContext.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.TvShowId != null)
                .Select(favorite => favorite.TvShowId!.Value))
            .Union(dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => item.TvShowId != null)
                .Select(item => item.TvShowId!.Value))
            .Union(watchingTvShowIds);
    }
}
