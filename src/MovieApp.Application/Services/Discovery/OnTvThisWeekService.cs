using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Discovery;

public sealed class OnTvThisWeekService(
    IOnTvThisWeekCatalog onTvThisWeekCatalog,
    ITvShowRepository tvShowRepository,
    ICacheService cacheService,
    ILogger<OnTvThisWeekService> logger) : IOnTvThisWeekService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public async Task<PaginatedResult<SearchItem>> GetOnTvThisWeekAsync(
        OnTvThisWeekCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var validation = OnTvThisWeekValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = OnTvThisWeekCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<DiscoveryCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        TvShowProviderSearchResult searchResult;
        try
        {
            searchResult = await onTvThisWeekCatalog.GetOnTheAirTvShowsAsync(
                criteria.Page,
                cancellationToken);
        }
        catch (Exception exception)
        {
            OnTvThisWeekLogMessages.LogProviderFailed(logger, criteria.Page, exception);
            throw new SearchProviderUnavailableException();
        }

        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(searchResult.Results, cancellationToken);
        var items = MapTvResults(searchResult.Results, tvIds);

        var result = DiscoverBrowseMerger.CreateSingleTypeResult(
            items,
            criteria.Page,
            criteria.PageSize,
            searchResult.TotalCount);

        await cacheService.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = result },
            CacheTtl,
            cancellationToken);

        return result;
    }

    private static List<SearchItem> MapTvResults(
        IReadOnlyList<TvShowProviderSummary> summaries,
        IReadOnlyDictionary<int, Guid> tvIds)
    {
        var items = new List<SearchItem>();

        foreach (var summary in summaries)
        {
            if (summary.TmdbId is null ||
                !tvIds.TryGetValue(summary.TmdbId.Value, out var id))
            {
                continue;
            }

            items.Add(ProviderSearchMapper.ToSearchItem(summary, id));
        }

        return items;
    }
}
