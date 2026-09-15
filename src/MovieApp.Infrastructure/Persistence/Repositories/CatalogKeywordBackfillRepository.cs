using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogKeywordBackfillRepository(ApplicationDbContext dbContext) : ICatalogKeywordBackfillRepository
{
    public async Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectMovieCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        var interactedMovieIds = BuildInteractedMovieIdsQuery();
        var query = dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.TmdbId != null &&
                            movie.TmdbId > 0 &&
                            movie.KeywordsSyncedAtUtc == null);

        if (excludeIds.Count > 0)
        {
            query = query.Where(movie => !excludeIds.Contains(movie.Id));
        }

        return await query
            .OrderByDescending(movie => interactedMovieIds.Contains(movie.Id))
            .ThenByDescending(movie => movie.VoteCount)
            .ThenBy(movie => movie.Id)
            .Take(limit)
            .Select(movie => new CatalogKeywordBackfillCandidate(
                movie.Id,
                "movie",
                movie.TmdbId!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectTvShowCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        var interactedTvShowIds = BuildInteractedTvShowIdsQuery();
        var query = dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId != null &&
                             tvShow.TmdbId > 0 &&
                             tvShow.KeywordsSyncedAtUtc == null);

        if (excludeIds.Count > 0)
        {
            query = query.Where(tvShow => !excludeIds.Contains(tvShow.Id));
        }

        return await query
            .OrderByDescending(tvShow => interactedTvShowIds.Contains(tvShow.Id))
            .ThenByDescending(tvShow => tvShow.VoteCount)
            .ThenBy(tvShow => tvShow.Id)
            .Take(limit)
            .Select(tvShow => new CatalogKeywordBackfillCandidate(
                tvShow.Id,
                "tv",
                tvShow.TmdbId!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogKeywordCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default)
    {
        var movieEligible = await dbContext.Movies
            .AsNoTracking()
            .CountAsync(movie => movie.TmdbId != null && movie.TmdbId > 0, cancellationToken);

        var movieSynced = await dbContext.Movies
            .AsNoTracking()
            .CountAsync(
                movie => movie.TmdbId != null &&
                         movie.TmdbId > 0 &&
                         movie.KeywordsSyncedAtUtc != null,
                cancellationToken);

        var tvEligible = await dbContext.TvShows
            .AsNoTracking()
            .CountAsync(tvShow => tvShow.TmdbId != null && tvShow.TmdbId > 0, cancellationToken);

        var tvSynced = await dbContext.TvShows
            .AsNoTracking()
            .CountAsync(
                tvShow => tvShow.TmdbId != null &&
                          tvShow.TmdbId > 0 &&
                          tvShow.KeywordsSyncedAtUtc != null,
                cancellationToken);

        var movieUnsynced = movieEligible - movieSynced;
        var tvUnsynced = tvEligible - tvSynced;
        var overallEligible = movieEligible + tvEligible;
        var overallSynced = movieSynced + tvSynced;
        var overallUnsynced = overallEligible - overallSynced;

        return new CatalogKeywordCoverageSnapshot(
            movieEligible,
            movieSynced,
            movieUnsynced,
            CalculateCoveragePercent(movieSynced, movieEligible),
            tvEligible,
            tvSynced,
            tvUnsynced,
            CalculateCoveragePercent(tvSynced, tvEligible),
            overallEligible,
            overallSynced,
            overallUnsynced,
            CalculateCoveragePercent(overallSynced, overallEligible));
    }

    public Task<bool> IsMovieKeywordSyncedAsync(Guid movieId, CancellationToken cancellationToken = default) =>
        dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.Id == movieId)
            .Select(movie => movie.KeywordsSyncedAtUtc != null)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsTvShowKeywordSyncedAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
        dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.Id == tvShowId)
            .Select(tvShow => tvShow.KeywordsSyncedAtUtc != null)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<Guid> BuildInteractedMovieIdsQuery() =>
        dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.MovieId != null)
            .Select(favorite => favorite.MovieId!.Value)
            .Union(dbContext.Ratings
                .AsNoTracking()
                .Where(rating => rating.MovieId != null)
                .Select(rating => rating.MovieId!.Value))
            .Union(dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => item.MovieId != null)
                .Select(item => item.MovieId!.Value))
            .Union(dbContext.WatchedMovies
                .AsNoTracking()
                .Select(item => item.MovieId));

    private IQueryable<Guid> BuildInteractedTvShowIdsQuery() =>
        dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.TvShowId != null)
            .Select(favorite => favorite.TvShowId!.Value)
            .Union(dbContext.Ratings
                .AsNoTracking()
                .Where(rating => rating.TvShowId != null)
                .Select(rating => rating.TvShowId!.Value))
            .Union(dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => item.TvShowId != null)
                .Select(item => item.TvShowId!.Value))
            .Union(dbContext.CatalogFollows
                .AsNoTracking()
                .Where(follow => follow.ContentType == CatalogContentType.Tv)
                .Select(follow => follow.ContentId))
            .Union(dbContext.WatchedEpisodes
                .AsNoTracking()
                .Select(item => item.Episode.Season.TvShowId));

    private static decimal CalculateCoveragePercent(int synced, int eligible) =>
        eligible == 0 ? 0m : Math.Round(synced * 100m / eligible, 2, MidpointRounding.AwayFromZero);
}
