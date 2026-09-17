using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Application.Services.Insights;

public static class InsightsRatingsAnalyticsBuilder
{
    public const int MostUsedStarsMinimumRatings = 5;

    public static InsightsRatingsAnalyticsResult Build(InsightsAnalyticsRawData raw)
    {
        var ratingCount = raw.RatingsCount;
        if (ratingCount == 0)
        {
            return new InsightsRatingsAnalyticsResult(
                0,
                null,
                [],
                null);
        }

        var distribution = ProfileStatisticsBuilder
            .BuildStarDistribution(raw.RatingScoreCounts)
            .Select(item => new InsightsRatingsDistributionItemResult(item.Stars, item.Count))
            .ToList();

        var average = ProfileStatisticsBuilder.CalculateAverageStarRating(raw.RatingScoreCounts);
        int? mostUsedStars = null;
        if (ratingCount >= MostUsedStarsMinimumRatings)
        {
            mostUsedStars = distribution
                .OrderByDescending(item => item.Count)
                .ThenByDescending(item => item.Stars)
                .FirstOrDefault(item => item.Count > 0)?.Stars;
        }

        return new InsightsRatingsAnalyticsResult(
            ratingCount,
            average,
            distribution,
            mostUsedStars);
    }
}
