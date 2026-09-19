using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

public sealed class HomeGlobalSectionsProvider(
    IDiscoveryService discoveryService,
    ICacheService cacheService,
    ISearchRefreshLockService refreshLockService,
    IOptions<HomeOptions> options) : IHomeGlobalSectionsProvider
{
    private static readonly TimeSpan GlobalCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CachePollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly int MaxCachePollAttempts = 50;

    private readonly HomeOptions _options = options.Value;

    public async Task<HomeGlobalSections> GetOrLoadAsync(
        HomeCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var genreFingerprint = HomeGlobalCacheKeys.CreateGenreFingerprint(_options.GenreSections);
        var cacheKey = HomeGlobalCacheKeys.Create(criteria.Type, criteria.SectionSize, genreFingerprint);
        var cached = await cacheService.GetAsync<HomeGlobalCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return ToSections(cached);
        }

        var lockKey = HomeGlobalCacheLockKeys.Create(cacheKey);
        var lockHandle = await refreshLockService.TryAcquireAsync(lockKey, LockDuration, cancellationToken);

        if (lockHandle is null)
        {
            var waited = await WaitForCachedSectionsAsync(cacheKey, cancellationToken);
            if (waited is not null)
            {
                return waited;
            }
        }

        try
        {
            cached = await cacheService.GetAsync<HomeGlobalCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return ToSections(cached);
            }

            var sections = await BuildGlobalSectionsAsync(criteria, cancellationToken);
            await cacheService.SetAsync(
                cacheKey,
                ToEntry(sections),
                GlobalCacheTtl,
                cancellationToken);

            return sections;
        }
        finally
        {
            if (lockHandle is not null)
            {
                await refreshLockService.ReleaseAsync(
                    lockHandle.LockKey,
                    lockHandle.LockToken,
                    lockHandle.Backend,
                    cancellationToken);
            }
        }
    }

    private async Task<HomeGlobalSections?> WaitForCachedSectionsAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCachePollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(CachePollInterval, cancellationToken);

            var cached = await cacheService.GetAsync<HomeGlobalCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return ToSections(cached);
            }
        }

        return null;
    }

    private async Task<HomeGlobalSections> BuildGlobalSectionsAsync(
        HomeCriteria criteria,
        CancellationToken cancellationToken)
    {
        var discoveryCriteria = new DiscoveryCriteria(criteria.Type, 1, criteria.SectionSize);

        var trending = await HomeSectionBuilders.BuildDiscoverySectionAsync(
            HomeSectionType.Trending,
            "Trending",
            discoveryService.GetTrendingAsync(
                discoveryCriteria,
                ContentLocaleResolver.EnglishUnitedStates,
                cancellationToken),
            criteria,
            cancellationToken);
        var popular = await HomeSectionBuilders.BuildDiscoverySectionAsync(
            HomeSectionType.Popular,
            "Popular",
            discoveryService.GetPopularAsync(
                discoveryCriteria,
                ContentLocaleResolver.EnglishUnitedStates,
                cancellationToken),
            criteria,
            cancellationToken);
        var newReleases = await HomeSectionBuilders.BuildDiscoverySectionAsync(
            HomeSectionType.NewReleases,
            "New Releases",
            discoveryService.GetNewReleasesAsync(
                discoveryCriteria,
                ContentLocaleResolver.EnglishUnitedStates,
                cancellationToken),
            criteria,
            cancellationToken);
        var topRated = await HomeSectionBuilders.BuildDiscoverySectionAsync(
            HomeSectionType.TopRated,
            "Top Rated",
            discoveryService.GetTopRatedAsync(
                discoveryCriteria,
                ContentLocaleResolver.EnglishUnitedStates,
                cancellationToken),
            criteria,
            cancellationToken);
        var genreSections = await HomeSectionBuilders.BuildGenreSectionsAsync(
            discoveryService,
            _options.GenreSections,
            criteria,
            cancellationToken);

        return new HomeGlobalSections(trending, popular, newReleases, topRated, genreSections);
    }

    private static HomeGlobalSections ToSections(HomeGlobalCacheEntry entry) =>
        new(entry.Trending, entry.Popular, entry.NewReleases, entry.TopRated, entry.GenreSections);

    private static HomeGlobalCacheEntry ToEntry(HomeGlobalSections sections) =>
        new()
        {
            Trending = sections.Trending,
            Popular = sections.Popular,
            NewReleases = sections.NewReleases,
            TopRated = sections.TopRated,
            GenreSections = sections.GenreSections.ToList()
        };
}
