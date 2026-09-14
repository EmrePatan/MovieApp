using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.WatchProviders;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetTvShowWatchProvidersService(
    ITvShowRepository tvShowRepository,
    IWatchProviderService watchProviderService,
    ICacheService cacheService) : IGetTvShowWatchProvidersService
{
    private static readonly TimeSpan WatchProvidersCacheTtl = TimeSpan.FromHours(6);

    public async Task<WatchProvidersResult> GetWatchProvidersAsync(
        Guid tvShowId,
        string? region,
        CancellationToken cancellationToken = default)
    {
        var regionValidation = WatchProviderRegionValidator.Validate(region);
        if (!regionValidation.IsValid)
        {
            throw new ValidationException(regionValidation.ErrorMessage!);
        }

        var normalizedRegion = WatchProviderRegionValidator.Normalize(region);

        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        if (tvShow.TmdbId is null)
        {
            return new WatchProvidersResult(normalizedRegion, [], null);
        }

        var cacheKey = TvShowWatchProvidersCacheKeys.Create(tvShowId, normalizedRegion);
        var cached = await cacheService.GetAsync<WatchProvidersCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var providers = await watchProviderService.GetTvShowWatchProvidersAsync(
            tvShow.TmdbId.Value,
            normalizedRegion,
            cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new WatchProvidersCacheEntry { Result = providers },
            WatchProvidersCacheTtl,
            cancellationToken);

        return providers;
    }
}
