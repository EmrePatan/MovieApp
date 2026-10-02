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
        var skip = Math.Max(0, (criteria.Page - 1) * criteria.PageSize);
        var ordered = criteria.Mode == DiscoverBrowseMode.Popular &&
                      effectiveSort is DiscoverBrowseSort.PopularityDesc or DiscoverBrowseSort.PopularityAsc
            ? Interleave(movieItems, tvItems)
            : DiscoverBrowseSorter.Sort(movieItems.Concat(tvItems), effectiveSort);
        var mergedItems = ordered
            .Skip(skip)
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

    private static List<SearchItem> Interleave(
        IReadOnlyList<SearchItem> movieItems,
        IReadOnlyList<SearchItem> tvItems)
    {
        var merged = new List<SearchItem>(movieItems.Count + tvItems.Count);
        var count = Math.Max(movieItems.Count, tvItems.Count);
        for (var index = 0; index < count; index++)
        {
            if (index < movieItems.Count)
            {
                merged.Add(movieItems[index]);
            }

            if (index < tvItems.Count)
            {
                merged.Add(tvItems[index]);
            }
        }

        return merged;
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
