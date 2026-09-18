using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3EraBuilder
{
    public static InsightsV3EraSectionResult Build(InsightsV3RawData raw)
    {
        var eras = InsightsErasBuilder.Build(raw.MilestoneRaw);
        var favoriteDecade = eras.Buckets
            .Where(bucket => bucket.Count > 0)
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Bucket, StringComparer.Ordinal)
            .Select(bucket => bucket.Bucket)
            .FirstOrDefault();

        InsightsV3OldestTitleResult? oldestTitle = null;
        if (raw.OldestTitle is not null)
        {
            oldestTitle = new InsightsV3OldestTitleResult(
                raw.OldestTitle.ContentType,
                raw.OldestTitle.ContentId,
                raw.OldestTitle.Title,
                raw.OldestTitle.Year,
                raw.OldestTitle.PosterPath);
        }

        return new InsightsV3EraSectionResult(
            eras.Buckets,
            favoriteDecade,
            eras.UnknownCount,
            oldestTitle);
    }
}
