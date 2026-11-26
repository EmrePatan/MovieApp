using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class TvShowSeasonSummaryHydrator(
    ITvShowRepository tvShowRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICatalogKeywordReadPathScheduler catalogKeywordReadPathScheduler) : ITvShowSeasonSummaryHydrator
{
    private static readonly TimeSpan RunningShowMaxAge = TimeSpan.FromDays(7);

    public async Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        // Stored seasons are enough unless the extras were never fetched or a running show has aged.
        var hasSeasons = tvShow.Seasons.Any(season => season.SeasonNumber >= 1);
        if (hasSeasons && (tvShow.TmdbId is null || (tvShow.DetailExtrasJson is not null && !IsOutdated(tvShow))))
        {
            ScheduleKeywordsIfNeeded(tvShow);
            return new TvShowSeasonSummaryHydrationResult(tvShow, ProviderCatalogRefreshed: false);
        }

        var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
        if (externalId is null)
        {
            ScheduleKeywordsIfNeeded(tvShow);
            return new TvShowSeasonSummaryHydrationResult(tvShow, ProviderCatalogRefreshed: false);
        }

        TvShowProviderDetails? details;
        try
        {
            details = await tvShowDataProvider.GetTvShowAsync(
                externalId,
                includeKeywords: true,
                cancellationToken);
        }
        catch (Exception) when (hasSeasons && !cancellationToken.IsCancellationRequested)
        {
            // The stored show can still be shown, so a provider failure must not break the page.
            details = null;
        }

        if (details is null)
        {
            ScheduleKeywordsIfNeeded(tvShow);
            return new TvShowSeasonSummaryHydrationResult(tvShow, ProviderCatalogRefreshed: false);
        }

        var hydratedTvShow = await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
            details,
            enrichKeywords: true,
            cancellationToken);
        return new TvShowSeasonSummaryHydrationResult(hydratedTvShow, ProviderCatalogRefreshed: true);
    }

    // Background sync covers followed and discovery titles only; this covers the rest on view.
    private static bool IsOutdated(TvShow tvShow) =>
        tvShow.Status is not (TvShowStatus.Ended or TvShowStatus.Canceled) &&
        (tvShow.TmdbMetadataUpdatedAtUtc is null ||
         tvShow.TmdbMetadataUpdatedAtUtc < DateTime.UtcNow - RunningShowMaxAge);

    private void ScheduleKeywordsIfNeeded(TvShow tvShow)
    {
        if (tvShow.KeywordsSyncedAtUtc is null)
        {
            catalogKeywordReadPathScheduler.ScheduleTvShow(tvShow.Id);
        }
    }
}
