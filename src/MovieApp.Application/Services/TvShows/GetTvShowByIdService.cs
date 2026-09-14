using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.TvShows;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowByIdService(
    ITvShowSeasonSummaryHydrator seasonSummaryHydrator,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    ICacheService cacheService) : IGetTvShowByIdService
{
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<TvShowDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = TvShowDetailsCacheKeys.Create(id);
        var cachedEntry = await cacheService.GetAsync<TvShowDetailsCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null && cachedEntry.Result.Seasons.Count > 0)
        {
            return cachedEntry.Result;
        }

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

        return result;
    }
}
