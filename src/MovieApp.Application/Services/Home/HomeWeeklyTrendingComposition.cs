using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

internal static class HomeWeeklyTrendingComposition
{
    internal static (List<SearchItem> HeroItems, List<SearchItem> TrendingItems) Split(
        IReadOnlyList<SearchItem> weeklyOrdered,
        int heroSize,
        int trendingSize)
    {
        if (heroSize <= 0 && trendingSize <= 0)
        {
            return ([], []);
        }

        var heroItems = weeklyOrdered.Take(heroSize).ToList();
        var heroKeys = heroItems
            .Select(HomeWeeklyTrendingContentKey.FromSearchItem)
            .ToHashSet();

        var trendingItems = weeklyOrdered
            .Where(item => !heroKeys.Contains(HomeWeeklyTrendingContentKey.FromSearchItem(item)))
            .Take(trendingSize)
            .ToList();

        return (heroItems, trendingItems);
    }
}
