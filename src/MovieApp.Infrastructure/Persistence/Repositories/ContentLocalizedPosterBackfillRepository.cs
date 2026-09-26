using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ContentLocalizedPosterBackfillRepository(ApplicationDbContext dbContext)
    : IContentLocalizedPosterBackfillRepository
{
    public async Task<IReadOnlyList<ContentLocalizedPosterBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
        Guid? startAfterMovieId,
        Guid? startAfterTvShowId,
        CancellationToken cancellationToken = default)
    {
        var languageKey = SupportedArtworkLanguageKeys.Turkish;
        var movieTarget = batchSize / 2;
        var tvTarget = batchSize - movieTarget;
        var selected = new List<ContentLocalizedPosterBackfillCandidate>(batchSize);

        selected.AddRange(await SelectMovieCandidatesAsync(movieTarget, startAfterMovieId, languageKey, cancellationToken));
        selected.AddRange(await SelectTvCandidatesAsync(tvTarget, startAfterTvShowId, languageKey, cancellationToken));

        var remaining = batchSize - selected.Count;
        if (remaining <= 0)
        {
            return selected;
        }

        if (selected.Count(candidate => candidate.ContentType == CatalogContentType.Movie) < movieTarget)
        {
            selected.AddRange(await SelectTvCandidatesAsync(remaining, startAfterTvShowId, languageKey, cancellationToken));
        }
        else
        {
            selected.AddRange(await SelectMovieCandidatesAsync(remaining, startAfterMovieId, languageKey, cancellationToken));
        }

        return selected.Take(batchSize).ToList();
    }

    private async Task<IReadOnlyList<ContentLocalizedPosterBackfillCandidate>> SelectMovieCandidatesAsync(
        int take,
        Guid? startAfterMovieId,
        string languageKey,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return [];
        }

        var query = dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.TmdbId != null)
            .Where(movie => !dbContext.Set<ContentLocalizedPoster>().Any(row =>
                row.ContentType == CatalogContentType.Movie
                && row.ContentId == movie.Id
                && row.LanguageKey == languageKey));

        if (startAfterMovieId is not null)
        {
            query = query.Where(movie => movie.Id.CompareTo(startAfterMovieId.Value) > 0);
        }

        return await query
            .OrderBy(movie => movie.Id)
            .Take(take)
            .Select(movie => new ContentLocalizedPosterBackfillCandidate(
                CatalogContentType.Movie,
                movie.Id,
                movie.TmdbId!.Value))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ContentLocalizedPosterBackfillCandidate>> SelectTvCandidatesAsync(
        int take,
        Guid? startAfterTvShowId,
        string languageKey,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return [];
        }

        var query = dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId != null)
            .Where(tvShow => !dbContext.Set<ContentLocalizedPoster>().Any(row =>
                row.ContentType == CatalogContentType.Tv
                && row.ContentId == tvShow.Id
                && row.LanguageKey == languageKey));

        if (startAfterTvShowId is not null)
        {
            query = query.Where(tvShow => tvShow.Id.CompareTo(startAfterTvShowId.Value) > 0);
        }

        return await query
            .OrderBy(tvShow => tvShow.Id)
            .Take(take)
            .Select(tvShow => new ContentLocalizedPosterBackfillCandidate(
                CatalogContentType.Tv,
                tvShow.Id,
                tvShow.TmdbId!.Value))
            .ToListAsync(cancellationToken);
    }
}
