using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.TvShows;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetSeasonService(
    ITvShowRepository tvShowRepository,
    ISeasonRepository seasonRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    ICacheService cacheService) : IGetSeasonService
{
    private static readonly TimeSpan SeasonCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<SeasonResult> GetSeasonAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        if (seasonNumber < 1)
        {
            throw new ValidationException("Season number must be at least 1.");
        }

        var cacheKey = TvShowSeasonCacheKeys.Create(tvShowId, seasonNumber);
        var cachedEntry = await cacheService.GetAsync<TvShowSeasonCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var identity = await tvShowRepository.GetExternalIdsByIdAsync(tvShowId, cancellationToken);
        if (identity is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        var season = await seasonRepository.GetByTvShowIdAndSeasonNumberAsync(tvShowId, seasonNumber, cancellationToken);
        var providerCatalogRefreshed = false;
        if (season is null || season.Episodes.Count == 0)
        {
            var externalId = externalIdResolver.Resolve(identity.TmdbId, identity.TvdbId, identity.ImdbId);
            if (externalId is null)
            {
                throw new NotFoundException(
                    $"Season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }

            var providerSeason = await tvShowDataProvider.GetSeasonAsync(
                externalId,
                seasonNumber,
                cancellationToken);

            if (providerSeason is null)
            {
                throw new NotFoundException(
                    $"Season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }

            season = await seasonRepository.UpsertFromProviderAsync(tvShowId, providerSeason, cancellationToken);
            providerCatalogRefreshed = true;
        }

        if (providerCatalogRefreshed)
        {
            await catalogSyncStateService.MarkRefreshedAsync(
                tvShowId,
                TvShowCatalogRefreshReason.DetailHydration,
                DateTime.UtcNow,
                cancellationToken);
        }

        var result = TvShowMapper.ToSeasonResult(season);

        await cacheService.SetAsync(
            cacheKey,
            new TvShowSeasonCacheEntry { Result = result },
            SeasonCacheTtl,
            cancellationToken);

        return result;
    }
}
