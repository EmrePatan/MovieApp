using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3RecordsBuilder
{
    public static InsightsV3RecordsSectionResult Build(InsightsV3RawData raw, TimeZoneInfo timeZone)
    {
        var highestRatingStars = InsightsV3RatingsBuilder.CalculateHighestRatingStars(raw.RatingScoreCounts);

        return new InsightsV3RecordsSectionResult(
            raw.Records.LongestStreakDays,
            raw.Records.BestMovieWeek,
            raw.Records.BestEpisodeWeek,
            highestRatingStars);
    }
}
