using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Library;

internal static class LibrarySearchAggregator
{
    public const int FetchPageSize = 100;
    public const int MaxPagesPerCategory = 15;
    public const int MaxCollectedItems = 750;

    private static readonly LibraryCategory[] SearchCategories =
    [
        LibraryCategory.Watching,
        LibraryCategory.Watched,
        LibraryCategory.Liked,
        LibraryCategory.Watchlist,
    ];

    public static IReadOnlyList<LibraryCategory> Categories => SearchCategories;

    public static IReadOnlyList<LibraryItemResult> MergeByTitlePriority(
        IReadOnlyDictionary<LibraryCategory, IReadOnlyList<LibraryItemResult>> categoryResults)
    {
        var seen = new HashSet<(string Type, Guid Id)>();
        var merged = new List<LibraryItemResult>();

        foreach (var category in SearchCategories)
        {
            if (!categoryResults.TryGetValue(category, out var items))
            {
                items = [];
            }

            foreach (var item in items)
            {
                var key = (item.Type, item.Id);
                if (!seen.Add(key))
                {
                    continue;
                }

                merged.Add(item);
            }
        }

        return merged
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToList();
    }

    public static PaginatedResult<LibraryItemResult> Paginate(
        IReadOnlyList<LibraryItemResult> items,
        int page,
        int pageSize)
    {
        var totalCount = items.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (page - 1) * pageSize;
        var pageItems = items.Skip(skip).Take(pageSize).ToList();

        return new PaginatedResult<LibraryItemResult>(
            pageItems,
            page,
            pageSize,
            totalCount,
            totalPages,
            null,
            page < totalPages);
    }
}
