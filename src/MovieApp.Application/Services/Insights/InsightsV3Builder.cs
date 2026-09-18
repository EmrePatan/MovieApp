using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3Builder
{
    public static InsightsV3Result Build(
        InsightsV3RawData raw,
        TimeZoneInfo timeZone,
        int year,
        DateTime utcNow)
    {
        var movieDna = InsightsV3MovieDnaBuilder.Build(raw, utcNow);
        var yourYear = InsightsV3YourYearBuilder.Build(raw, timeZone, year);
        var yourTaste = InsightsV3TasteBuilder.Build(raw);
        var timeInStories = InsightsV3TimeInStoriesBuilder.Build(raw);
        var yourRatings = InsightsV3RatingsBuilder.Build(raw);
        var yourEra = InsightsV3EraBuilder.Build(raw);
        var yourRecords = InsightsV3RecordsBuilder.Build(raw, timeZone);
        var achievements = InsightsMilestonesBuilder.Build(raw.MilestoneRaw);

        return new InsightsV3Result(
            new InsightsV3MetaResult(raw.MemberSinceUtc, utcNow, timeZone.Id, year),
            movieDna,
            yourYear,
            yourTaste,
            timeInStories,
            yourRatings,
            yourEra,
            yourRecords,
            achievements);
    }
}
