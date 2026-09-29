using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

internal static class HomeWeeklyTrendingComposition
{
    internal const int MinimumHeroVoteCount = 50;

    internal const decimal MinimumHeroVoteAverage = 6.5m;

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
        !string.IsNullOrWhiteSpace(item.BackdropUrl) &&
        item.VoteAverage >= MinimumHeroVoteAverage &&
        item.VoteCount >= MinimumHeroVoteCount;

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
