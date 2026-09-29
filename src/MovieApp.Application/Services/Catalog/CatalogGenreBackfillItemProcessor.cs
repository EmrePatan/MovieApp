using System.Globalization;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.MovieChanges;

namespace MovieApp.Application.Services.Catalog;

public sealed class CatalogGenreBackfillItemProcessor(
    IMovieChangesTargetedRefreshService movieChangesTargetedRefreshService,
    ITvShowRepository tvShowRepository,
    ITvShowDataProvider tvShowDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICatalogGenreBackfillRepository backfillRepository) : ICatalogGenreBackfillItemProcessor
{
    public async Task<CatalogGenreBackfillItemOutcome> ProcessAsync(
        CatalogGenreBackfillCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        if (candidate.ContentType == "movie")
        {
            return await ProcessMovieAsync(candidate, cancellationToken);
        }

        if (candidate.ContentType == "tv")
        {
            return await ProcessTvShowAsync(candidate, cancellationToken);
        }

        return CatalogGenreBackfillItemOutcome.Skipped;
    }

    private async Task<CatalogGenreBackfillItemOutcome> ProcessMovieAsync(
        CatalogGenreBackfillCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (await backfillRepository.MovieHasGenresAsync(candidate.CatalogId, cancellationToken))
        {
            return CatalogGenreBackfillItemOutcome.Skipped;
        }

        var refreshResult = await movieChangesTargetedRefreshService.RefreshRelevantMovieAsync(
            candidate.CatalogId,
            cancellationToken);

        if (refreshResult.Outcome is TmdbChangesTargetRefreshOutcome.SkippedNotFound or
            TmdbChangesTargetRefreshOutcome.SkippedUnavailable)
        {
            return CatalogGenreBackfillItemOutcome.Skipped;
        }

        if (refreshResult.Outcome == TmdbChangesTargetRefreshOutcome.Failed)
        {
            return CatalogGenreBackfillItemOutcome.Failed;
        }

        return await backfillRepository.MovieHasGenresAsync(candidate.CatalogId, cancellationToken)
            ? CatalogGenreBackfillItemOutcome.Succeeded
            : CatalogGenreBackfillItemOutcome.Unrepairable;
    }

    private async Task<CatalogGenreBackfillItemOutcome> ProcessTvShowAsync(
        CatalogGenreBackfillCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (await backfillRepository.TvShowHasGenresAsync(candidate.CatalogId, cancellationToken))
        {
            return CatalogGenreBackfillItemOutcome.Skipped;
        }

        var tvShow = await tvShowRepository.GetByIdAsync(candidate.CatalogId, cancellationToken);
        if (tvShow is null || !tvShow.TmdbId.HasValue)
        {
            return CatalogGenreBackfillItemOutcome.Skipped;
        }

        var providerDetails = await tvShowDataProvider.GetTvShowAsync(
            tvShow.TmdbId.Value.ToString(CultureInfo.InvariantCulture),
            includeKeywords: false,
            cancellationToken);

        if (providerDetails is null)
        {
            return CatalogGenreBackfillItemOutcome.Skipped;
        }

        await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
            providerDetails,
            enrichKeywords: false,
            cancellationToken);

        return await backfillRepository.TvShowHasGenresAsync(candidate.CatalogId, cancellationToken)
            ? CatalogGenreBackfillItemOutcome.Succeeded
            : CatalogGenreBackfillItemOutcome.Unrepairable;
    }
}
