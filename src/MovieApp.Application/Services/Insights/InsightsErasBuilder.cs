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
        var counts = raw.MovieTitles
            .Select(title => title.ReleaseYear)
            .Concat(raw.TvShowTitles.Select(title => title.ReleaseYear))
            .GroupBy(year => year)
            .Select(group => new InsightsV3ReleaseYearCount(group.Key, group.Count()))
            .ToList();

        return BuildFromYearCounts(counts);
    }

    public static InsightsErasResult BuildFromYearCounts(IReadOnlyList<InsightsV3ReleaseYearCount> years)
    {
        var bucketCounts = KnownBucketOrder.ToDictionary(bucket => bucket, _ => 0);
        var unknownCount = 0;

        foreach (var row in years)
        {
            if (row.Year is null)
            {
                unknownCount += row.Count;
                continue;
            }

            var bucket = MapYearToBucket(row.Year.Value);
            bucketCounts[bucket] += row.Count;
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
