using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.StreamingLeaving;

public interface IStreamingLeavingFetchService
{
    Task FetchAsync(string region, CancellationToken cancellationToken = default);
}

public sealed partial class StreamingLeavingFetchService(
    IStreamingLeavingRepository repository,
    IStreamingAvailabilityProvider provider,
    IStreamingLeavingTitleResolver titleResolver,
    ISearchRefreshLockService lockService,
    ICacheService cacheService,
    IOptions<StreamingAvailabilityOptions> options,
    TimeProvider timeProvider,
    ILogger<StreamingLeavingFetchService> logger) : IStreamingLeavingFetchService
{
    // Adding a few hundred new titles to the catalog takes minutes.
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(30);

    public async Task FetchAsync(string region, CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsOperational)
        {
            return;
        }

        var handle = await lockService.TryAcquireAsync(
            $"streaming-leaving:{region}",
            LockDuration,
            cancellationToken);
        if (handle is null)
        {
            return;
        }

        try
        {
            await FetchLockedAsync(region, cancellationToken);
        }
        finally
        {
            // Also after a failure: the attempt time and the stored rows may have changed.
            await cacheService.RemoveAsync(StreamingLeavingCache.Key(region), CancellationToken.None);
            await lockService.ReleaseAsync(handle.LockKey, handle.LockToken, handle.Backend, CancellationToken.None);
        }
    }

    private async Task FetchLockedAsync(string region, CancellationToken cancellationToken)
    {
        var policy = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var periodStart = StreamingLeavingSchedule.PeriodStart(now, policy);
        var state = await repository.GetRegionAsync(region, cancellationToken);

        if (!StreamingLeavingSchedule.IsDue(state, policy, now))
        {
            return;
        }

        var usage = await repository.GetUsageAsync(periodStart, cancellationToken);
        if (!StreamingLeavingSchedule.HasQuota(usage, region, policy))
        {
            return;
        }

        state ??= new StreamingLeavingRegion { Region = region };
        state.LastAttemptAtUtc = now;
        state.LastFetchRequestCount = 0;
        await repository.SaveRegionAsync(state, cancellationToken);

        var download = await DownloadAsync(state, periodStart, now, cancellationToken);

        if (download.NoDataForRegion)
        {
            // Remembered as a success, so the region is not asked again until the next refresh.
            await repository.ReplaceItemsAsync(region, [], cancellationToken);
            state.IsComplete = true;
            state.LastSuccessAtUtc = now;
            await repository.SaveRegionAsync(state, cancellationToken);
            return;
        }

        // Keep the old list when we got nothing, or when an error cut the new one short.
        if (download.PageCount == 0 ||
            (download.Failed && await repository.HasUpcomingAsync(region, now, cancellationToken)))
        {
            await repository.SaveRegionAsync(state, cancellationToken);
            return;
        }

        var items = await BuildItemsAsync(region, download, now, cancellationToken);
        await repository.ReplaceItemsAsync(region, items, cancellationToken);

        state.IsComplete = download.Complete;
        if (!download.Failed)
        {
            state.LastSuccessAtUtc = now;
        }

        await repository.SaveRegionAsync(state, cancellationToken);
        LogFetched(logger, region, items.Count, state.LastFetchRequestCount, download.Complete);
    }

    private async Task<Download> DownloadAsync(
        StreamingLeavingRegion state,
        DateOnly periodStart,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var policy = options.Value;
        var download = new Download();

        // Reserve before calling: a request that fails still costs quota.
        async Task<bool> TryReserveAsync()
        {
            if (state.LastFetchRequestCount >= policy.MaxRequestsPerFetch ||
                !await repository.TryReserveRequestAsync(
                    periodStart,
                    state.Region,
                    current => StreamingLeavingSchedule.HasQuota(current, state.Region, policy),
                    cancellationToken))
            {
                return false;
            }

            state.LastFetchRequestCount++;
            return true;
        }

        try
        {
            if (state.ServicesJson is not null &&
                state.ServicesFetchedAtUtc is { } fetchedAt &&
                now - fetchedAt < TimeSpan.FromDays(policy.ServiceListCacheDays))
            {
                download.Services = JsonSerializer.Deserialize<List<StreamingService>>(state.ServicesJson) ?? [];
            }
            else
            {
                if (!await TryReserveAsync())
                {
                    return download;
                }

                download.Services = await provider.GetSubscriptionServicesAsync(state.Region, cancellationToken);
                state.ServicesJson = JsonSerializer.Serialize(download.Services);
                state.ServicesFetchedAtUtc = now;
            }

            if (download.Services.Count == 0)
            {
                download.NoDataForRegion = true;
                return download;
            }

            var serviceIds = download.Services.Select(service => service.Id).ToList();
            string? cursor = null;
            while (await TryReserveAsync())
            {
                var page = await provider.GetExpiringAsync(state.Region, serviceIds, cursor, cancellationToken);
                download.PageCount++;
                download.Changes.AddRange(page.Changes);
                cursor = page.NextCursor;
                if (cursor is null)
                {
                    download.Complete = true;
                    break;
                }
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            download.Failed = true;
            LogFetchFailed(logger, exception, state.Region, state.LastFetchRequestCount);
        }

        return download;
    }

    private async Task<IReadOnlyList<StreamingLeavingItem>> BuildItemsAsync(
        string region,
        Download download,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // The same title can appear more than once for a service; the nearest date wins.
        var nearest = download.Changes
            .Where(change => change.LeavesAtUtc > now)
            .GroupBy(change => (change.ServiceId, change.Title))
            .Select(group => group.MinBy(change => change.LeavesAtUtc)!)
            .ToList();

        var contentIds = await titleResolver.ResolveAsync(
            nearest.Select(change => change.Title).Distinct().ToList(),
            cancellationToken);
        var serviceNames = download.Services.ToDictionary(service => service.Id, service => service.Name);

        return nearest
            .Where(change => contentIds.ContainsKey(change.Title))
            // Two TMDB ids can point at one catalog title; the table allows one row per service.
            .GroupBy(change => (change.ServiceId, change.Title.MediaType, ContentId: contentIds[change.Title]))
            .Select(group =>
            {
                var change = group.MinBy(candidate => candidate.LeavesAtUtc)!;
                return new StreamingLeavingItem
                {
                    Id = Guid.NewGuid(),
                    Region = region,
                    ServiceId = change.ServiceId,
                    ServiceName = serviceNames.GetValueOrDefault(change.ServiceId, change.ServiceId),
                    MediaType = change.Title.MediaType,
                    TmdbId = change.Title.TmdbId,
                    ContentId = group.Key.ContentId,
                    LeavesAtUtc = change.LeavesAtUtc,
                    WatchLink = change.WatchLink,
                    FetchedAtUtc = now
                };
            })
            .ToList();
    }

    private sealed class Download
    {
        public IReadOnlyList<StreamingService> Services { get; set; } = [];

        public List<StreamingLeavingChange> Changes { get; } = [];

        public int PageCount { get; set; }

        public bool Complete { get; set; }

        public bool Failed { get; set; }

        public bool NoDataForRegion { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Leaving-soon fetch failed for {Region} after {RequestCount} requests.")]
    private static partial void LogFetchFailed(ILogger logger, Exception exception, string region, int requestCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Leaving-soon fetch stored {ItemCount} rows for {Region} using {RequestCount} requests (complete: {Complete}).")]
    private static partial void LogFetched(ILogger logger, string region, int itemCount, int requestCount, bool complete);
}
