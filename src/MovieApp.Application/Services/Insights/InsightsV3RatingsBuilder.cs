using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3RatingsBuilder
{
    public const int MinimumGenreRatings = 3;

    public static InsightsV3RatingsSectionResult Build(InsightsV3RawData raw)
    {
        var baseRatings = InsightsRatingsAnalyticsBuilder.Build(raw.MilestoneRaw);
        var eligibleGenres = raw.GenreRatings
            .Where(genre => genre.RatingCount >= MinimumGenreRatings)
            .Select(ToGenreRatingResult)
            .ToList();

        InsightsV3GenreRatingResult? highest = null;
        InsightsV3GenreRatingResult? lowest = null;

        if (eligibleGenres.Count >= 2)
        {
            highest = eligibleGenres
                .OrderByDescending(genre => genre.AverageStars)
                .ThenBy(genre => genre.Name, StringComparer.Ordinal)
                .First();
        }

        if (eligibleGenres.Count >= 1)
        {
            lowest = eligibleGenres
                .OrderBy(genre => genre.AverageStars)
                .ThenBy(genre => genre.Name, StringComparer.Ordinal)
                .First();
        }

        return new InsightsV3RatingsSectionResult(
            baseRatings.RatingCount,
            baseRatings.AverageStarRating,
            baseRatings.Distribution,
            highest,
            lowest);
    }

    public static InsightsV3GenreRatingResult ToGenreRatingResult(InsightsV3GenreRatingRow row) =>
        new(
            row.GenreId,
            row.Name,
            row.RatingCount,
            Math.Round(row.AverageScore / 2m, 1, MidpointRounding.AwayFromZero));

    public static decimal? CalculateHighestRatingStars(IReadOnlyList<(int Score, int Count)> ratingScoreCounts)
    {
        if (ratingScoreCounts.Count == 0)
        {
            return null;
        }

        var maxScore = ratingScoreCounts.Max(item => item.Score);
        return Math.Round(maxScore / 2m, 1, MidpointRounding.AwayFromZero);
    }
}
