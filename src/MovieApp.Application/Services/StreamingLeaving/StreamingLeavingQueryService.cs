using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.StreamingLeaving;

public interface IStreamingLeavingQueryService
{
    Task<StreamingLeavingResult> GetAsync(
        StreamingLeavingCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}

public sealed class StreamingLeavingQueryService(
    IStreamingLeavingRepository repository,
    IStreamingLeavingFetchJobEnqueuer fetchJobEnqueuer,
    SearchItemCatalogMetadataEnricher metadataEnricher,
    CatalogSearchItemDisplayTitleEnricher displayTitleEnricher,
    ICacheService cacheService,
    IOptions<StreamingAvailabilityOptions> options,
    TimeProvider timeProvider) : IStreamingLeavingQueryService
{
    public async Task<StreamingLeavingResult> GetAsync(
        StreamingLeavingCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var policy = options.Value;
        if (!policy.IsOperational)
        {
            return StreamingLeavingResult.Empty(criteria.Page, criteria.PageSize);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cacheKey = StreamingLeavingCache.Key(criteria.Region);
        var cached = await cacheService.GetAsync<StreamingLeavingCacheEntry>(cacheKey, cancellationToken);
        var entry = cached ?? new StreamingLeavingCacheEntry
        {
            Rows = await repository.GetUpcomingAsync(criteria.Region, now, cancellationToken),
            State = await repository.GetRegionAsync(criteria.Region, cancellationToken)
        };

        // The caller never waits for the service: a due fetch runs in the background.
        var isDue = StreamingLeavingSchedule.IsDue(entry.State, policy, now);

        // A list that is about to be fetched is not worth keeping.
        if (cached is null && !isDue)
        {
            await cacheService.SetAsync(cacheKey, entry, StreamingLeavingCache.Ttl, cancellationToken);
        }

        if (isDue)
        {
            var usage = await repository.GetUsageAsync(
                StreamingLeavingSchedule.PeriodStart(now, policy),
                cancellationToken);
            if (StreamingLeavingSchedule.HasQuota(usage, criteria.Region, policy))
            {
                fetchJobEnqueuer.EnqueueFetch(criteria.Region);
            }
        }

        // A cached list can outlive some of its dates.
        var rows = entry.Rows.Where(row => row.LeavesAtUtc > now).ToList();
        var result = StreamingLeavingListBuilder.Build(rows, criteria, policy.MinimumItems);
        if (result.Items.Count == 0)
        {
            return result;
        }

        var page = new PaginatedResult<SearchItem>(
            result.Items.Select(item => item.Item).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);
        page = await metadataEnricher.EnrichGenresAsync(page, cancellationToken);
        page = await displayTitleEnricher.EnrichAsync(page, contentLocale, cancellationToken);

        return result with
        {
            Items = result.Items.Select((item, index) => item with { Item = page.Items[index] }).ToList()
        };
    }
}
