using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

/// <summary>
/// Achievement score: every unlocked tier is worth a fixed number of points
/// (bronze 1, silver 2, gold 3, platinum 5, legend 8). The client turns the total into a level.
/// </summary>
public static class AchievementPoints
{
    private static readonly int[] TierPoints = [1, 2, 3, 5, 8];

    public static int ForTier(int tier) =>
        TierPoints[Math.Clamp(tier, 1, TierPoints.Length) - 1];

    public static int Sum(IEnumerable<InsightsMilestoneResult> achievements) =>
        achievements.Where(item => item.Achieved).Sum(item => ForTier(item.Tier));
}
