using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.Home;

public sealed class HotThisWeekTrendingSnapshotService(
    ITrendingWeekDataProvider trendingWeekDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ICacheService cacheService,
    IOptions<HotThisWeekTrendingRefreshOptions> options,
    ILogger<HotThisWeekTrendingSnapshotService> logger) : IHotThisWeekTrendingSnapshotService
{
    private readonly HotThisWeekTrendingRefreshOptions _options = options.Value;

    public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        cacheService.GetAsync<HotThisWeekTrendingSnapshotEntry>(
            HotThisWeekTrendingSnapshotCacheKeys.Canonical,
            cancellationToken);

    public async Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TrendingWeekProviderItem> providerItems;

        try
        {
            providerItems = await trendingWeekDataProvider.GetTrendingWeekAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            HotThisWeekTrendingSnapshotLogMessages.LogRefreshProviderFailed(logger, exception);
            return new HotThisWeekTrendingSnapshotRefreshResult(false, 0, 0, 0);
        }

        if (providerItems.Count == 0)
        {
            HotThisWeekTrendingSnapshotLogMessages.LogRefreshSkippedNoUsableData(
                logger,
                providerItems.Count,
                0,
                providerItems.Count);
            return new HotThisWeekTrendingSnapshotRefreshResult(false, 0, 0, providerItems.Count);
        }

        var movieSummaries = providerItems
            .Where(item => item.MediaType == "movie")
            .Select(TrendingWeekCatalogMapper.ToMovieSummary)
            .ToList();
        var tvSummaries = providerItems
            .Where(item => item.MediaType == "tv")
            .Select(TrendingWeekCatalogMapper.ToTvSummary)
            .ToList();

        var movieIds = await movieRepository.EnsureFromSummariesAsync(movieSummaries, cancellationToken);
        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(tvSummaries, cancellationToken);
        var mappedItems = TrendingWeekCatalogMapper.MapOrderedItems(providerItems, movieIds, tvIds);
        var skippedItemCount = providerItems.Count - mappedItems.Count;

        if (mappedItems.Count == 0)
        {
            HotThisWeekTrendingSnapshotLogMessages.LogRefreshSkippedNoUsableData(
                logger,
                providerItems.Count,
                0,
                skippedItemCount);
            return new HotThisWeekTrendingSnapshotRefreshResult(
                false,
                providerItems.Count,
                0,
                skippedItemCount);
        }

        var entry = new HotThisWeekTrendingSnapshotEntry
        {
            RefreshedAt = DateTimeOffset.UtcNow,
            Items = mappedItems,
        };

        await cacheService.SetAsync(
            HotThisWeekTrendingSnapshotCacheKeys.Canonical,
            entry,
            TimeSpan.FromDays(_options.SnapshotTtlDays),
            cancellationToken);

        HotThisWeekTrendingSnapshotLogMessages.LogRefreshCompleted(
            logger,
            providerItems.Count,
            mappedItems.Count,
            skippedItemCount,
            entry.RefreshedAt);

        return new HotThisWeekTrendingSnapshotRefreshResult(
            true,
            providerItems.Count,
            mappedItems.Count,
            skippedItemCount);
    }
}
