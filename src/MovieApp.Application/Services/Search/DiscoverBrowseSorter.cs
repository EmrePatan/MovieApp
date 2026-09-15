using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class DiscoverBrowseSorter
{
    public static IReadOnlyList<SearchItem> Sort(
        IEnumerable<SearchItem> items,
        DiscoverBrowseSort sort)
    {
        IOrderedEnumerable<SearchItem> ordered = sort switch
        {
            DiscoverBrowseSort.PopularityAsc => items
                .OrderBy(item => item.VoteCount)
                .ThenBy(item => item.VoteAverage),
            DiscoverBrowseSort.RatingDesc => items
                .OrderByDescending(item => item.VoteAverage)
                .ThenByDescending(item => item.VoteCount),
            DiscoverBrowseSort.RatingAsc => items
                .OrderBy(item => item.VoteAverage)
                .ThenBy(item => item.VoteCount),
            DiscoverBrowseSort.ReleaseDesc => items
                .OrderByDescending(item => item.ReleaseDate ?? DateOnly.MinValue)
                .ThenByDescending(item => item.VoteAverage),
            DiscoverBrowseSort.ReleaseAsc => items
                .OrderBy(item => item.ReleaseDate ?? DateOnly.MaxValue)
                .ThenBy(item => item.VoteAverage),
            DiscoverBrowseSort.TitleAsc => items.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            DiscoverBrowseSort.TitleDesc => items.OrderByDescending(item => item.Title, StringComparer.OrdinalIgnoreCase),
            _ => items
                .OrderByDescending(item => item.VoteCount)
                .ThenByDescending(item => item.VoteAverage)
        };

        return ordered
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToList();
    }
}
