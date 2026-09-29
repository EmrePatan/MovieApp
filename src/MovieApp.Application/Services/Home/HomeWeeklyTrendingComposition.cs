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

        var heroItems = SelectHeroItems(weeklyOrdered, heroSize);
        var heroKeys = heroItems
            .Select(HomeWeeklyTrendingContentKey.FromSearchItem)
            .ToHashSet();

        var trendingItems = weeklyOrdered
            .Where(item => !heroKeys.Contains(HomeWeeklyTrendingContentKey.FromSearchItem(item)))
            .Take(trendingSize)
            .ToList();

        return (heroItems, trendingItems);
    }

    internal static bool IsHeroEligible(SearchItem item) =>
        !string.IsNullOrWhiteSpace(item.BackdropUrl);

    private static List<SearchItem> SelectHeroItems(IReadOnlyList<SearchItem> weeklyOrdered, int heroSize)
    {
        if (heroSize <= 0)
        {
            return [];
        }

        var heroItems = new List<SearchItem>(heroSize);
        foreach (var item in weeklyOrdered)
        {
            if (heroItems.Count >= heroSize)
            {
                break;
            }

            if (IsHeroEligible(item))
            {
                heroItems.Add(item);
            }
        }

        return heroItems;
    }
}
