using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
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
        var rows = await repository.GetUpcomingAsync(criteria.Region, now, cancellationToken);

        // The caller never waits for the service: a due fetch runs in the background.
        var state = await repository.GetRegionAsync(criteria.Region, cancellationToken);
        if (StreamingLeavingSchedule.IsDue(state, policy, now))
        {
            var usage = await repository.GetUsageAsync(
                StreamingLeavingSchedule.PeriodStart(now, policy),
                cancellationToken);
            if (StreamingLeavingSchedule.HasQuota(usage, criteria.Region, policy))
            {
                fetchJobEnqueuer.EnqueueFetch(criteria.Region);
            }
        }

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
