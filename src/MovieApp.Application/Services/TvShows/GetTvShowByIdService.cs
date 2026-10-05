using System.Globalization;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowByIdService(
    ITvShowSeasonSummaryHydrator seasonSummaryHydrator,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    ICacheService cacheService,
    IContentLocalizedPosterRepository contentLocalizedPosterRepository,
    IContentLocalizedPosterSynchronizer contentLocalizedPosterSynchronizer,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdLookup externalIdLookup,
    IDetailLocalizationOverlayService detailLocalizationOverlayService,
    IDetailKeywordOverlayService detailKeywordOverlayService) : IGetTvShowByIdService
{
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromMinutes(15);

    public Task<TvShowDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetByIdCoreAsync(id, contentLocale: null, cancellationToken);

    public Task<TvShowDetailsResult> GetByIdAsync(
        Guid id,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        GetByIdCoreAsync(id, contentLocale, cancellationToken);

    private async Task<TvShowDetailsResult> GetByIdCoreAsync(
        Guid id,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        var cacheKey = TvShowDetailsCacheKeys.Create(id);
        var cachedEntry = await cacheService.GetAsync<TvShowDetailsCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null && cachedEntry.Result.Seasons.Count > 0)
        {
            return await FinalizeTvShowDetailsAsync(id, cachedEntry.Result, contentLocale, cancellationToken);
        }

        var overlayTask = await StartTvShowOverlayAsync(id, contentLocale, cancellationToken);
        var hydrationResult = await seasonSummaryHydrator.EnsureSeasonSummariesAsync(id, cancellationToken);
        if (hydrationResult.ProviderCatalogRefreshed)
        {
            await catalogSyncStateService.MarkRefreshedAsync(
                id,
                TvShowCatalogRefreshReason.DetailHydration,
                DateTime.UtcNow,
                cancellationToken);
        }

        var result = TvShowMapper.ToDetailsResult(hydrationResult.TvShow);

        await cacheService.SetAsync(
            cacheKey,
            new TvShowDetailsCacheEntry { Result = result },
            DetailsCacheTtl,
            cancellationToken);

        if (overlayTask is null)
        {
            return await FinalizeTvShowDetailsAsync(id, result, contentLocale, cancellationToken);
        }

        var localized = detailLocalizationOverlayService.ApplyLoadedTvShowOverlay(
            result,
            await overlayTask,
            contentLocale);
        localized = await ApplyLocalizedPosterOverlayAsync(id, localized, contentLocale, cancellationToken);
        return await detailKeywordOverlayService.ApplyTvShowKeywordsAsync(
            id,
            localized,
            contentLocale,
            cancellationToken);
    }

    private async Task<TvShowDetailsResult> FinalizeTvShowDetailsAsync(
        Guid tvShowId,
        TvShowDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        result = await ApplyOverlayAsync(tvShowId, result, contentLocale, cancellationToken);
        return await detailKeywordOverlayService.ApplyTvShowKeywordsAsync(
            tvShowId,
            result,
            contentLocale,
            cancellationToken);
    }

    private async Task<Task<TvShowDetailLocalizationData?>?> StartTvShowOverlayAsync(
        Guid id,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        if (contentLocale is null)
        {
            return null;
        }

        var identity = await externalIdLookup.GetAsync(id, cancellationToken);
        return detailLocalizationOverlayService.LoadTvShowOverlayAsync(
            identity?.TmdbId,
            contentLocale,
            cancellationToken);
    }

    private async Task<TvShowDetailsResult> ApplyOverlayAsync(
        Guid tvShowId,
        TvShowDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        if (contentLocale is not null)
        {
            result = await detailLocalizationOverlayService.ApplyTvShowOverlayAsync(
                result,
                contentLocale,
                cancellationToken);
        }

        return await ApplyLocalizedPosterOverlayAsync(tvShowId, result, contentLocale, cancellationToken);
    }

    private async Task<TvShowDetailsResult> ApplyLocalizedPosterOverlayAsync(
        Guid tvShowId,
        TvShowDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        if (contentLocale is null ||
            !ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return result;
        }

        var key = new ContentLocalizedPosterKey(CatalogContentType.Tv, tvShowId);
        await EnsureLocalizedPosterPersistedAsync(
            tvShowId,
            result.TmdbId,
            result.PosterPath,
            contentLocale,
            result.OriginalLanguage,
            result.PrimaryOriginCountryCode,
            result.OriginalTitle,
            cancellationToken);

        var localizedPosters = await LocalizedPosterDisplayOverlay.LoadPosterPathsAsync(
            contentLocalizedPosterRepository,
            [key],
            contentLocale,
            cancellationToken);

        var posterPath = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
            result.PosterPath,
            key,
            localizedPosters,
            contentLocale,
            new ContentProductionContext(result.OriginalLanguage, result.PrimaryOriginCountryCode));

        return result with { PosterPath = posterPath };
    }

    private async Task EnsureLocalizedPosterPersistedAsync(
        Guid tvShowId,
        int? tmdbId,
        string? canonicalPosterPath,
        string contentLocale,
        string? originalLanguage,
        string? primaryOriginCountryCode,
        string? originalTitle,
        CancellationToken cancellationToken)
    {
        var languageKey = SupportedArtworkLanguageKeys.ResolvePosterLanguageKey(contentLocale);
        if (languageKey is null)
        {
            return;
        }

        var key = new ContentLocalizedPosterKey(CatalogContentType.Tv, tvShowId);
        var existing = await contentLocalizedPosterRepository.GetPosterPathsAsync(
            [key],
            languageKey,
            cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        if (!ContentProductionHeuristics.IsTurkishProduction(
                originalLanguage,
                primaryOriginCountryCode,
                originalTitle))
        {
            return;
        }

        var missKey = TvLocalizedPosterMissCacheKeys.Create(tvShowId, languageKey);
        if (await cacheService.GetAsync<TvLocalizedPosterMissCacheEntry>(missKey, cancellationToken) is not null)
        {
            return;
        }

        var resolvedTmdbId = tmdbId;
        if (resolvedTmdbId is not > 0)
        {
            var identity = await externalIdLookup.GetAsync(tvShowId, cancellationToken);
            resolvedTmdbId = identity?.TmdbId;
        }

        if (resolvedTmdbId is not > 0)
        {
            return;
        }

        var providerDetails = await tvShowDataProvider.GetTvShowAsync(
            resolvedTmdbId.Value.ToString(CultureInfo.InvariantCulture),
            includeKeywords: false,
            cancellationToken);
        if (providerDetails is null)
        {
            await RememberLocalizedPosterMissAsync(missKey, cancellationToken);
            return;
        }

        await contentLocalizedPosterSynchronizer.SyncFromProviderPostersAsync(
            CatalogContentType.Tv,
            tvShowId,
            providerDetails.PosterPath ?? canonicalPosterPath,
            providerDetails.ProviderPosters,
            providerDetails.OriginalLanguage,
            providerDetails.PrimaryOriginCountryCode,
            cancellationToken);

        var persisted = await contentLocalizedPosterRepository.GetPosterPathsAsync(
            [key],
            languageKey,
            cancellationToken);
        if (persisted.Count == 0)
        {
            await RememberLocalizedPosterMissAsync(missKey, cancellationToken);
        }
    }

    private Task RememberLocalizedPosterMissAsync(string missKey, CancellationToken cancellationToken) =>
        cacheService.SetAsync(
            missKey,
            new TvLocalizedPosterMissCacheEntry(),
            TvLocalizedPosterMissCacheKeys.Ttl,
            cancellationToken);
}
