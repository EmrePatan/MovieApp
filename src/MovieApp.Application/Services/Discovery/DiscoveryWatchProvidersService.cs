using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.WatchProviders;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Discovery;

public sealed class DiscoveryWatchProvidersService(
    IDiscoveryWatchProviderCatalog watchProviderCatalog,
    ICacheService cacheService,
    IOptions<AdvancedDiscoverOptions>? advancedDiscoverOptions = null) : IDiscoveryWatchProvidersService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
        SearchContentType mediaType,
        string watchRegion,
        CancellationToken cancellationToken = default) =>
        GetWatchProvidersAsync(mediaType, watchRegion, includeTransactionalProviders: false, cancellationToken);

    public async Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
        SearchContentType mediaType,
        string watchRegion,
        bool includeTransactionalProviders,
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
            return FilterForCatalog(cached.Providers, includeTransactionalProviders);
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

        return FilterForCatalog(providers, includeTransactionalProviders);
    }

    private IReadOnlyList<DiscoveryWatchProviderItem> FilterForCatalog(
        IReadOnlyList<DiscoveryWatchProviderItem> providers,
        bool includeTransactionalProviders)
    {
        var transactionalProviderIds = TransactionalWatchProviderCatalog.Resolve(
            advancedDiscoverOptions?.Value.TransactionalWatchProviderIds);

        return DiscoveryWatchProviderListFilter.Apply(
            providers,
            transactionalProviderIds,
            includeTransactionalProviders);
    }
}
