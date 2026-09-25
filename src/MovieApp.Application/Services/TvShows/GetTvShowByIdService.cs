using MovieApp.Application.Abstractions.Caching;
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
    ITvShowExternalIdLookup? externalIdLookup = null,
    IDetailLocalizationOverlayService? detailLocalizationOverlayService = null) : IGetTvShowByIdService
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
            return await ApplyOverlayAsync(cachedEntry.Result, contentLocale, cancellationToken);
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

        if (overlayTask is null || detailLocalizationOverlayService is null)
        {
            return result;
        }

        return detailLocalizationOverlayService.ApplyLoadedTvShowOverlay(result, await overlayTask);
    }

    private async Task<Task<TvShowDetailLocalizationData?>?> StartTvShowOverlayAsync(
        Guid id,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        if (detailLocalizationOverlayService is null ||
            externalIdLookup is null ||
            contentLocale is null)
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
        TvShowDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        if (detailLocalizationOverlayService is null || contentLocale is null)
        {
            return result;
        }

        return await detailLocalizationOverlayService.ApplyTvShowOverlayAsync(
            result,
            contentLocale,
            cancellationToken);
    }
}
