using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Keywords;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class MdbListKeywordBackfillRepository(ApplicationDbContext dbContext) : IMdbListKeywordBackfillRepository
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

        var query = dbContext.Movies
            .AsNoTracking()
            .Where(movie =>
                movie.TmdbId != null &&
                movie.TmdbId > 0 &&
                movie.MdbListKeywordsSyncedAtUtc == null);

        if (excludeIds.Count > 0)
        {
            query = query.Where(movie => !excludeIds.Contains(movie.Id));
        }

        return await query
            .OrderByDescending(movie => movie.VoteCount)
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

        var query = dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow =>
                tvShow.TmdbId != null &&
                tvShow.TmdbId > 0 &&
                tvShow.MdbListKeywordsSyncedAtUtc == null);

        if (excludeIds.Count > 0)
        {
            query = query.Where(tvShow => !excludeIds.Contains(tvShow.Id));
        }

        return await query
            .OrderByDescending(tvShow => tvShow.VoteCount)
            .ThenBy(tvShow => tvShow.Id)
            .Take(limit)
            .Select(tvShow => new CatalogKeywordBackfillCandidate(
                tvShow.Id,
                "tv",
                tvShow.TmdbId!.Value))
            .ToListAsync(cancellationToken);
    }
}
