using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ContentSearchTitleProviderEnrichmentRepository(ApplicationDbContext dbContext)
    : IContentSearchTitleProviderEnrichmentRepository
{
    public Task<int> CountMoviesWithTmdbIdAsync(CancellationToken cancellationToken = default) =>
        dbContext.Movies.AsNoTracking().CountAsync(movie => movie.TmdbId != null, cancellationToken);

    public Task<int> CountTvShowsWithResolvableProviderIdAsync(CancellationToken cancellationToken = default) =>
        dbContext.TvShows.AsNoTracking().CountAsync(
            tvShow => tvShow.TmdbId != null || tvShow.TvdbId != null || tvShow.ImdbId != null,
            cancellationToken);

    public async Task<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>> SelectMovieCandidatesAsync(
        Guid? startAfterId,
        Guid? onlyMovieId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Movies.AsNoTracking().Where(movie => movie.TmdbId != null);

        if (onlyMovieId is not null)
        {
            query = query.Where(movie => movie.Id == onlyMovieId.Value);
        }
        else if (startAfterId is not null)
        {
            query = query.Where(movie => movie.Id.CompareTo(startAfterId.Value) > 0);
        }

        return await query
            .OrderBy(movie => movie.Id)
            .Take(take)
            .Select(movie => new ContentSearchTitleEnrichmentCandidate(
                CatalogContentType.Movie,
                movie.Id,
                movie.TmdbId,
                movie.Title))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>> SelectTvShowCandidatesAsync(
        Guid? startAfterId,
        Guid? onlyTvShowId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TvShows.AsNoTracking()
            .Where(tvShow => tvShow.TmdbId != null || tvShow.TvdbId != null || tvShow.ImdbId != null);

        if (onlyTvShowId is not null)
        {
            query = query.Where(tvShow => tvShow.Id == onlyTvShowId.Value);
        }
        else if (startAfterId is not null)
        {
            query = query.Where(tvShow => tvShow.Id.CompareTo(startAfterId.Value) > 0);
        }

        var rows = await query
            .OrderBy(tvShow => tvShow.Id)
            .Take(take)
            .Select(tvShow => new
            {
                tvShow.Id,
                tvShow.TmdbId,
                tvShow.Title,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ContentSearchTitleEnrichmentCandidate(
                CatalogContentType.Tv,
                row.Id,
                row.TmdbId,
                row.Title))
            .ToList();
    }

    public async Task<Guid?> FindMovieIdByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        var normalized = title.Trim();
        var pattern = normalized.Contains('%', StringComparison.Ordinal)
            ? normalized
            : $"%{normalized}%";
        var movie = await dbContext.Movies.AsNoTracking()
            .Where(movie =>
                EF.Functions.ILike(movie.Title, pattern) ||
                (movie.OriginalTitle != null && EF.Functions.ILike(movie.OriginalTitle, pattern)))
            .OrderBy(movie => movie.Id)
            .Select(movie => new { movie.Id })
            .FirstOrDefaultAsync(cancellationToken);

        return movie?.Id;
    }

    public async Task<Guid?> FindMovieIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        var movie = await dbContext.Movies.AsNoTracking()
            .Where(movie => movie.TmdbId == tmdbId)
            .OrderBy(movie => movie.Id)
            .Select(movie => new { movie.Id })
            .FirstOrDefaultAsync(cancellationToken);

        return movie?.Id;
    }
}
