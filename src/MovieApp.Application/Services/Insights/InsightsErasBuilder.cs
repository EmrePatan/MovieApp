using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsErasBuilder
{
    private static readonly string[] KnownBucketOrder =
    [
        "2020s",
        "2010s",
        "2000s",
        "1990s",
        "Older"
    ];

    public static InsightsErasResult Build(InsightsAnalyticsRawData raw)
    {
        var years = raw.MovieTitles
            .Select(title => title.ReleaseYear)
            .Concat(raw.TvShowTitles.Select(title => title.ReleaseYear))
            .ToList();

        var bucketCounts = KnownBucketOrder.ToDictionary(bucket => bucket, _ => 0);
        var unknownCount = 0;

        foreach (var year in years)
        {
            if (year is null)
            {
                unknownCount++;
                continue;
            }

            var bucket = MapYearToBucket(year.Value);
            bucketCounts[bucket]++;
        }

        var knownTotal = bucketCounts.Values.Sum();
        var buckets = KnownBucketOrder
            .Select(bucket => new InsightsEraBucketResult(
                bucket,
                bucketCounts[bucket],
                knownTotal == 0
                    ? null
                    : Math.Round(bucketCounts[bucket] * 100m / knownTotal, 1, MidpointRounding.AwayFromZero)))
            .ToList();

        return new InsightsErasResult(buckets, unknownCount);
    }

    public static string MapYearToBucket(int year) =>
        year switch
        {
            >= 2020 => "2020s",
            >= 2010 => "2010s",
            >= 2000 => "2000s",
            >= 1990 => "1990s",
            _ => "Older"
        };
}
