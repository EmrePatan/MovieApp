using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.WatchProviders;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class TransactionalStreamOfferFilter(
    IWatchProviderService watchProviderService,
    ICacheService cacheService,
    IOptions<AdvancedDiscoverOptions> options) : ITransactionalStreamOfferFilter
{
    private static readonly TimeSpan OfferCacheTtl = TimeSpan.FromHours(6);
    private const int MaxConcurrentOfferLookups = 4;

    public async Task<IReadOnlySet<int>> SelectMatchingTmdbIdsAsync(
        SearchContentType mediaType,
        string? watchRegion,
        IReadOnlyList<int> watchProviderIds,
        IReadOnlyList<WatchMonetizationType> monetizationTypes,
        IReadOnlyList<int> tmdbIds,
        CancellationToken cancellationToken = default)
    {
        var transactionalProviderIds = TransactionalWatchProviderCatalog.Resolve(
            options.Value.TransactionalWatchProviderIds);

        if (!TransactionalWatchProviderCatalog.RequiresSameOfferStreamCheck(
                watchProviderIds,
                monetizationTypes,
                transactionalProviderIds) ||
            string.IsNullOrWhiteSpace(watchRegion) ||
            tmdbIds.Count == 0)
        {
            return tmdbIds.ToHashSet();
        }

        var selectedTransactionalIds = watchProviderIds
            .Where(transactionalProviderIds.Contains)
            .ToHashSet();
        var selectedSubscriptionIds = watchProviderIds
            .Where(providerId => !transactionalProviderIds.Contains(providerId))
            .ToHashSet();
        var region = WatchProviderRegionValidator.Normalize(watchRegion);
        var matching = new HashSet<int>();

        await Parallel.ForEachAsync(
            tmdbIds.Distinct(),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentOfferLookups,
                CancellationToken = cancellationToken
            },
            async (tmdbId, lookupCancellationToken) =>
            {
                var offers = await GetOffersAsync(mediaType, tmdbId, region, lookupCancellationToken);
                if (!HasRequestedStreamOffer(offers, selectedTransactionalIds, selectedSubscriptionIds))
                {
                    return;
                }

                lock (matching)
                {
                    matching.Add(tmdbId);
                }
            });

        return matching;
    }

    private async Task<WatchProvidersResult> GetOffersAsync(
        SearchContentType mediaType,
        int tmdbId,
        string region,
        CancellationToken cancellationToken)
    {
        var cacheKey = CreateCacheKey(mediaType, tmdbId, region);
        var cached = await cacheService.GetAsync<RawWatchOffersCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        var offers = mediaType == SearchContentType.Tv
            ? await watchProviderService.GetTvShowWatchProvidersAsync(tmdbId, region, cancellationToken)
            : await watchProviderService.GetMovieWatchProvidersAsync(tmdbId, region, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new RawWatchOffersCacheEntry { Result = offers },
            OfferCacheTtl,
            cancellationToken);

        return offers;
    }

    private static bool HasRequestedStreamOffer(
        WatchProvidersResult offers,
        HashSet<int> selectedTransactionalIds,
        HashSet<int> selectedSubscriptionIds)
    {
        if (selectedTransactionalIds.Any(providerId => HasFlatrate(offers, providerId)))
        {
            return true;
        }

        if (selectedSubscriptionIds.Count == 0)
        {
            return false;
        }

        return selectedSubscriptionIds.Any(providerId => HasFlatrate(offers, providerId));
    }

    private static bool HasFlatrate(WatchProvidersResult offers, int providerId) =>
        offers.Providers.Any(provider =>
            provider.ProviderId == providerId &&
            provider.AvailabilityTypes.Contains(WatchProviderAvailabilityType.Flatrate));

    private static string CreateCacheKey(SearchContentType mediaType, int tmdbId, string region)
    {
        var media = mediaType == SearchContentType.Tv ? "tv" : "movie";
        return $"raw-watch-offers:{media}:{tmdbId}:{region}:v1";
    }
}

public sealed class RawWatchOffersCacheEntry
{
    public required WatchProvidersResult Result { get; init; }
}
