namespace MovieApp.Application.Models.Catalog;

public sealed record CatalogMetadataFreshnessDistribution(
    int DiscoveryRelevantTotal,
    int FreshWithin24Hours,
    int Fresh24To72Hours,
    int StaleOver72Hours,
    int StaleOver7Days,
    int NeverRefreshed)
{
    public decimal FreshWithin72HoursPercent =>
        DiscoveryRelevantTotal == 0
            ? 100m
            : Math.Round(
                (FreshWithin24Hours + Fresh24To72Hours) * 100m / DiscoveryRelevantTotal,
                4,
                MidpointRounding.AwayFromZero);
}
