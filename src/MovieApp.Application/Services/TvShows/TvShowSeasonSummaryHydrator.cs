using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShows;

public sealed class TvShowSeasonSummaryHydrator(
    ITvShowRepository tvShowRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ICatalogKeywordReadPathScheduler catalogKeywordReadPathScheduler) : ITvShowSeasonSummaryHydrator
{
    public async Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        if (tvShow.Seasons.Any(season => season.SeasonNumber >= 1))
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

        var details = await tvShowDataProvider.GetTvShowAsync(
            externalId,
            includeKeywords: true,
            cancellationToken);
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

    private void ScheduleKeywordsIfNeeded(TvShow tvShow)
    {
        if (tvShow.KeywordsSyncedAtUtc is null)
        {
            catalogKeywordReadPathScheduler.ScheduleTvShow(tvShow.Id);
        }
    }
}
