using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Discovery;

public sealed class DiscoveryWatchProvidersService(
    IDiscoveryWatchProviderCatalog watchProviderCatalog,
    ICacheService cacheService) : IDiscoveryWatchProvidersService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
        SearchContentType mediaType,
        string watchRegion,
        CancellationToken cancellationToken = default)
    {
        var mediaTypeValidation = AdvancedDiscoverValidator.ValidateMediaType(mediaType);
        if (!mediaTypeValidation.IsValid)
        {
            throw new ValidationException(mediaTypeValidation.ErrorMessage!);
        }

        var regionValidation = AdvancedDiscoverValidator.ValidateWatchRegion(watchRegion, required: true);
        if (!regionValidation.IsValid)
        {
            throw new ValidationException(regionValidation.ErrorMessage!);
        }

        var normalizedRegion = WatchProviderRegionValidator.Normalize(watchRegion);
        var cacheKey = DiscoveryWatchProvidersCacheKeys.Create(mediaType, normalizedRegion);
        var cached = await cacheService.GetAsync<DiscoveryWatchProvidersCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Providers;
        }

        IReadOnlyList<DiscoveryWatchProviderItem> providers;
        try
        {
            providers = await watchProviderCatalog.GetWatchProvidersAsync(
                mediaType,
                normalizedRegion,
                cancellationToken);
        }
        catch (Exception exception) when (ProviderFailureFilter.IsProviderFailure(exception, cancellationToken))
        {
            throw new SearchProviderUnavailableException();
        }

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryWatchProvidersCacheEntry { Providers = providers },
            CacheTtl,
            cancellationToken);

        return providers;
    }
}
