using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

internal static class DiscoverBrowseMerger
{
    public static PaginatedResult<SearchItem> Merge(
        DiscoverBrowseCriteria criteria,
        IReadOnlyList<SearchItem> movieItems,
        IReadOnlyList<SearchItem> tvItems,
        int movieTotalCount,
        int tvTotalCount)
    {
        var effectiveSort = DiscoverBrowseValidator.GetEffectiveSort(criteria);
        var mergedItems = DiscoverBrowseSorter
            .Sort(movieItems.Concat(tvItems), effectiveSort)
            .Take(criteria.PageSize)
            .ToList();

        var totalCount = movieTotalCount + tvTotalCount;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)criteria.PageSize);

        return new PaginatedResult<SearchItem>(
            mergedItems,
            criteria.Page,
            criteria.PageSize,
            totalCount,
            totalPages);
    }

    public static PaginatedResult<SearchItem> CreateSingleTypeResult(
        IReadOnlyList<SearchItem> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var trimmedItems = items.Take(pageSize).ToList();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<SearchItem>(
            trimmedItems,
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}
