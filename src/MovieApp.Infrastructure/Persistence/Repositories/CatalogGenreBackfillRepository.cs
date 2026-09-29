using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Catalog;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogGenreBackfillRepository(ApplicationDbContext dbContext) : ICatalogGenreBackfillRepository
{
    public async Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectMovieCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        var query = dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.TmdbId != null &&
                            movie.TmdbId > 0 &&
                            !dbContext.MovieGenres.Any(link => link.MovieId == movie.Id));

        if (excludeIds.Count > 0)
        {
            query = query.Where(movie => !excludeIds.Contains(movie.Id));
        }

        return await query
            .OrderByDescending(movie => movie.VoteCount)
            .ThenBy(movie => movie.Id)
            .Take(limit)
            .Select(movie => new CatalogGenreBackfillCandidate(
                movie.Id,
                "movie",
                movie.TmdbId!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectTvShowCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        var query = dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId != null &&
                             tvShow.TmdbId > 0 &&
                             !dbContext.TvShowGenres.Any(link => link.TvShowId == tvShow.Id));

        if (excludeIds.Count > 0)
        {
            query = query.Where(tvShow => !excludeIds.Contains(tvShow.Id));
        }

        return await query
            .OrderByDescending(tvShow => tvShow.VoteCount)
            .ThenBy(tvShow => tvShow.Id)
            .Take(limit)
            .Select(tvShow => new CatalogGenreBackfillCandidate(
                tvShow.Id,
                "tv",
                tvShow.TmdbId!.Value))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> MovieHasGenresAsync(Guid movieId, CancellationToken cancellationToken = default) =>
        dbContext.MovieGenres.AsNoTracking().AnyAsync(link => link.MovieId == movieId, cancellationToken);

    public Task<bool> TvShowHasGenresAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
        dbContext.TvShowGenres.AsNoTracking().AnyAsync(link => link.TvShowId == tvShowId, cancellationToken);

    public async Task<CatalogGenreBackfillCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default)
    {
        var movieEligible = await dbContext.Movies
            .AsNoTracking()
            .CountAsync(movie => movie.TmdbId != null && movie.TmdbId > 0, cancellationToken);

        var movieGenreLess = await dbContext.Movies
            .AsNoTracking()
            .CountAsync(
                movie => movie.TmdbId != null &&
                         movie.TmdbId > 0 &&
                         !dbContext.MovieGenres.Any(link => link.MovieId == movie.Id),
                cancellationToken);

        var tvEligible = await dbContext.TvShows
            .AsNoTracking()
            .CountAsync(tvShow => tvShow.TmdbId != null && tvShow.TmdbId > 0, cancellationToken);

        var tvGenreLess = await dbContext.TvShows
            .AsNoTracking()
            .CountAsync(
                tvShow => tvShow.TmdbId != null &&
                          tvShow.TmdbId > 0 &&
                          !dbContext.TvShowGenres.Any(link => link.TvShowId == tvShow.Id),
                cancellationToken);

        return new CatalogGenreBackfillCoverageSnapshot(movieGenreLess, tvGenreLess, movieEligible, tvEligible);
    }
}
