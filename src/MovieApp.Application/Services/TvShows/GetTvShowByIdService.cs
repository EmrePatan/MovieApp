using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowByIdService(
    ITvShowSeasonSummaryHydrator seasonSummaryHydrator,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    ICacheService cacheService,
    IContentLocalizedPosterRepository contentLocalizedPosterRepository,
    ITvShowExternalIdLookup externalIdLookup,
    IDetailLocalizationOverlayService detailLocalizationOverlayService) : IGetTvShowByIdService
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
            return await ApplyOverlayAsync(id, cachedEntry.Result, contentLocale, cancellationToken);
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
            return await ApplyOverlayAsync(id, result, contentLocale, cancellationToken);
        }

        var localized = detailLocalizationOverlayService.ApplyLoadedTvShowOverlay(
            result,
            await overlayTask,
            contentLocale);
        return await ApplyOverlayAsync(id, localized, contentLocale, cancellationToken);
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
        var localizedPosters = await LocalizedPosterDisplayOverlay.LoadPosterPathsAsync(
            contentLocalizedPosterRepository,
            [key],
            contentLocale,
            cancellationToken);

        var posterPath = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
            result.PosterPath,
            key,
            localizedPosters);

        return result with { PosterPath = posterPath };
    }
}
