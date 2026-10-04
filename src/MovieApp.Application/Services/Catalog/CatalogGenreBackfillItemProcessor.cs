using System.Globalization;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.MovieChanges;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Catalog;

public sealed class CatalogGenreBackfillItemProcessor(
    IMovieChangesTargetedRefreshService movieChangesTargetedRefreshService,
    ITvShowRepository tvShowRepository,
    ITvShowDataProvider tvShowDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICatalogGenreBackfillRepository backfillRepository,
    IOptions<CatalogGenreBackfillOptions> options) : ICatalogGenreBackfillItemProcessor
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
            await backfillRepository.ClearRepairAttemptAsync(
                CatalogContentType.Movie,
                candidate.CatalogId,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.Skipped;
        }

        var refreshResult = await movieChangesTargetedRefreshService.RefreshRelevantMovieAsync(
            candidate.CatalogId,
            cancellationToken);

        if (refreshResult.Outcome is TmdbChangesTargetRefreshOutcome.SkippedNotFound or
            TmdbChangesTargetRefreshOutcome.SkippedUnavailable)
        {
            await RecordRepairAttemptAsync(
                CatalogContentType.Movie,
                candidate.CatalogId,
                CatalogGenreRepairAttemptOutcome.ProviderUnavailable,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.SkippedUnavailable;
        }

        if (refreshResult.Outcome == TmdbChangesTargetRefreshOutcome.Failed)
        {
            await RecordRepairAttemptAsync(
                CatalogContentType.Movie,
                candidate.CatalogId,
                CatalogGenreRepairAttemptOutcome.FailedTransient,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.Failed;
        }

        if (await backfillRepository.MovieHasGenresAsync(candidate.CatalogId, cancellationToken))
        {
            await backfillRepository.ClearRepairAttemptAsync(
                CatalogContentType.Movie,
                candidate.CatalogId,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.Succeeded;
        }

        if (refreshResult.ProviderReturnedUsableGenres)
        {
            await RecordRepairAttemptAsync(
                CatalogContentType.Movie,
                candidate.CatalogId,
                CatalogGenreRepairAttemptOutcome.FailedTransient,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.Failed;
        }

        await RecordRepairAttemptAsync(
            CatalogContentType.Movie,
            candidate.CatalogId,
            CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres,
            cancellationToken);
        return CatalogGenreBackfillItemOutcome.Unrepairable;
    }

    private async Task<CatalogGenreBackfillItemOutcome> ProcessTvShowAsync(
        CatalogGenreBackfillCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (await backfillRepository.TvShowHasGenresAsync(candidate.CatalogId, cancellationToken))
        {
            await backfillRepository.ClearRepairAttemptAsync(
                CatalogContentType.Tv,
                candidate.CatalogId,
                cancellationToken);
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
            await RecordRepairAttemptAsync(
                CatalogContentType.Tv,
                candidate.CatalogId,
                CatalogGenreRepairAttemptOutcome.ProviderUnavailable,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.SkippedUnavailable;
        }

        await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
            providerDetails,
            enrichKeywords: false,
            cancellationToken);

        if (await backfillRepository.TvShowHasGenresAsync(candidate.CatalogId, cancellationToken))
        {
            await backfillRepository.ClearRepairAttemptAsync(
                CatalogContentType.Tv,
                candidate.CatalogId,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.Succeeded;
        }

        if (CatalogProviderGenreNames.ContainsUsableGenreNames(providerDetails.Genres))
        {
            await RecordRepairAttemptAsync(
                CatalogContentType.Tv,
                candidate.CatalogId,
                CatalogGenreRepairAttemptOutcome.FailedTransient,
                cancellationToken);
            return CatalogGenreBackfillItemOutcome.Failed;
        }

        await RecordRepairAttemptAsync(
            CatalogContentType.Tv,
            candidate.CatalogId,
            CatalogGenreRepairAttemptOutcome.ProviderNoUsableGenres,
            cancellationToken);
        return CatalogGenreBackfillItemOutcome.Unrepairable;
    }

    private Task RecordRepairAttemptAsync(
        CatalogContentType contentType,
        Guid catalogId,
        CatalogGenreRepairAttemptOutcome outcome,
        CancellationToken cancellationToken)
    {
        var attemptedAtUtc = DateTime.UtcNow;
        var nextEligibleAtUtc = CatalogGenreBackfillRetryPolicy.CalculateNextEligibleAtUtc(
            outcome,
            attemptedAtUtc,
            options.Value);

        return backfillRepository.UpsertRepairAttemptAsync(
            contentType,
            catalogId,
            outcome,
            attemptedAtUtc,
            nextEligibleAtUtc,
            cancellationToken);
    }
}
