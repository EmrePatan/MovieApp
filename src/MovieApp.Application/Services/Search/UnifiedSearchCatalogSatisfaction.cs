using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class UnifiedSearchCatalogSatisfaction
{
    public static bool ShouldUseProviderFallback(SearchCriteria criteria, PaginatedResult<SearchItem> result)
    {
        if (string.IsNullOrWhiteSpace(criteria.Query))
        {
            return false;
        }

        return !CanSatisfyRequestedPage(result, criteria);
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

    public static bool ShouldCache(PaginatedResult<SearchItem> result) => result.TotalCount > 0;

    public static bool ShouldCacheAfterSearch(
        PaginatedResult<SearchItem> result,
        SearchCriteria criteria,
        bool providerIngestionSucceeded,
        bool providerIngestionFailed)
    {
        if (!ShouldCache(result))
        {
            return false;
        }

        if (providerIngestionFailed)
        {
            return false;
        }

        if (providerIngestionSucceeded)
        {
            return true;
        }

        return CanSatisfyRequestedPage(result, criteria);
    }
}
