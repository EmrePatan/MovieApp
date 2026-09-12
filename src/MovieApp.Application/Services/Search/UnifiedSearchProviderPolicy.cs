using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class UnifiedSearchProviderPolicy
{
    public static bool IsProviderRefreshScope(SearchCriteria criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria.Query))
        {
            return false;
        }

        if (criteria.Sort != SearchSortOption.Relevance)
        {
            return false;
        }

        if (criteria.GenreId.HasValue ||
            criteria.Year.HasValue ||
            criteria.MinRating.HasValue ||
            criteria.MaxRating.HasValue)
        {
            return false;
        }

        return true;
    }

    public static bool CanSatisfyRequestedPage(PaginatedResult<SearchItem> result, SearchCriteria criteria)
    {
        if (result.TotalCount <= (criteria.Page - 1) * criteria.PageSize)
        {
            return false;
        }

        if (criteria.Page == 1 && result.TotalCount < criteria.PageSize)
        {
            return false;
        }

        return true;
    }

    public static bool IsProviderRefreshStale(DateTime? lastRefreshedAtUtc, DateTime utcNow, TimeSpan refreshInterval)
    {
        if (lastRefreshedAtUtc is null)
        {
            return true;
        }

        return lastRefreshedAtUtc.Value.Add(refreshInterval) <= utcNow;
    }

    public static bool NeedsProviderRefresh(
        SearchCriteria criteria,
        PaginatedResult<SearchItem> result,
        DateTime? lastRefreshedAtUtc,
        DateTime utcNow,
        TimeSpan refreshInterval)
    {
        if (!IsProviderRefreshScope(criteria))
        {
            return !CanSatisfyRequestedPage(result, criteria);
        }

        if (!CanSatisfyRequestedPage(result, criteria))
        {
            return true;
        }

        return IsProviderRefreshStale(lastRefreshedAtUtc, utcNow, refreshInterval);
    }

    public static bool ShouldCache(PaginatedResult<SearchItem> result) => result.TotalCount > 0;

    public static bool ShouldCacheAfterSearch(
        PaginatedResult<SearchItem> result,
        SearchCriteria criteria,
        bool providerRefreshFullySucceeded,
        bool providerRefreshFailedOrPartial)
    {
        if (!ShouldCache(result))
        {
            return false;
        }

        if (providerRefreshFailedOrPartial)
        {
            return false;
        }

        if (providerRefreshFullySucceeded)
        {
            return true;
        }

        return CanSatisfyRequestedPage(result, criteria);
    }
}
