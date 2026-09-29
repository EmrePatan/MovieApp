using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

internal static class HomeWeeklyTrendingComposition
{
    internal static List<SearchItem> SelectTrendingItems(
        IReadOnlyList<SearchItem> weeklyOrdered,
        IReadOnlyList<SearchItem> heroItems,
        int trendingSize)
    {
        if (trendingSize <= 0)
        {
            return [];
        }

        var heroKeys = heroItems
            .Select(HomeWeeklyTrendingContentKey.FromSearchItem)
            .ToHashSet();

        return weeklyOrdered
            .Where(item => !heroKeys.Contains(HomeWeeklyTrendingContentKey.FromSearchItem(item)))
            .Take(trendingSize)
            .ToList();
    }
}
