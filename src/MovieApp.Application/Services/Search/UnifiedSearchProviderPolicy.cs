using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class UnifiedSearchProviderPolicy
{
    public static bool IsProviderScope(SearchCriteria criteria)
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

    public static bool ShouldCacheProviderResult(
        PaginatedResult<SearchItem> result,
        bool providerSucceeded)
    {
        return providerSucceeded;
    }

    public static bool ShouldCacheDbResult(PaginatedResult<SearchItem> result) =>
        result.TotalCount > 0;
}
